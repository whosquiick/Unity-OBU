#include <jni.h>
#include <string>
#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <unistd.h>
#include <fcntl.h>
#include <pthread.h>
#include <sys/ptrace.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <atomic>
#include <vector>

#define OBU_EXPORT __attribute__((visibility("default")))

// Native best-effort hardening for Android. Everything here raises the cost of attaching a
// debugger or a dumper; none of it is a hard stop on a rooted device. The C# side treats a
// missing library as "native checks unavailable" and falls back to the pure-C# probes.

namespace {

std::atomic<int> g_tamper_flags{0};

// Bit layout shared with the C# NativeGuard wrapper.
enum TamperBit {
    BIT_TRACER      = 1 << 0,
    BIT_FRIDA       = 1 << 1,
    BIT_HOOK        = 1 << 2,
    BIT_TEXT_CRC    = 1 << 3,
    BIT_DUMPER      = 1 << 4,
};

bool read_file(const char *path, std::string &out) {
    int fd = open(path, O_RDONLY);
    if (fd < 0) return false;
    char buf[4096];
    ssize_t n;
    out.clear();
    while ((n = read(fd, buf, sizeof(buf))) > 0) {
        out.append(buf, static_cast<size_t>(n));
    }
    close(fd);
    return true;
}

int read_tracer_pid() {
    std::string status;
    if (!read_file("/proc/self/status", status)) return -1;
    const char *key = "TracerPid:";
    size_t pos = status.find(key);
    if (pos == std::string::npos) return -1;
    pos += strlen(key);
    return atoi(status.c_str() + pos);
}

// Scan the memory map for tell-tale injector/hook libraries.
int scan_maps() {
    std::string maps;
    if (!read_file("/proc/self/maps", maps)) return 0;
    int flags = 0;
    static const char *frida[] = {"frida", "gum-js-loop", "gmain", "linjector", "frida-agent", "frida-gadget"};
    static const char *hook[]  = {"substrate", "xposed", "lsposed", "riru", "epic.", "whale", "dobby", "shadowhook"};
    static const char *dump[]  = {"il2cppdumper", "zygisk", "melonloader", "il2cpp_dumper", "sharprijndael"};
    for (const char *s : frida) if (maps.find(s) != std::string::npos) { flags |= BIT_FRIDA; break; }
    for (const char *s : hook)  if (maps.find(s) != std::string::npos) { flags |= BIT_HOOK; break; }
    for (const char *s : dump)  if (maps.find(s) != std::string::npos) { flags |= BIT_DUMPER; break; }
    return flags;
}

// ptrace self-attach: if we become our own tracer, a debugger cannot attach after us. If the
// attach fails, something is already tracing us.
void *trace_guard(void *) {
    pid_t child = fork();
    if (child == 0) {
        pid_t parent = getppid();
        if (ptrace(PTRACE_ATTACH, parent, nullptr, nullptr) != 0) {
            g_tamper_flags |= BIT_TRACER;
            _exit(0);
        }
        waitpid(parent, nullptr, 0);
        ptrace(PTRACE_CONT, parent, nullptr, nullptr);
        // Hold the trace for the life of the process.
        while (true) {
            int st;
            if (waitpid(parent, &st, 0) < 0) break;
            ptrace(PTRACE_CONT, parent, nullptr, nullptr);
        }
        _exit(0);
    }
    return nullptr;
}

// Background monitor: re-reads TracerPid and re-scans maps on an interval so an attach that
// happens after launch is still caught.
void *monitor(void *) {
    for (;;) {
        int pid = read_tracer_pid();
        if (pid > 0) g_tamper_flags |= BIT_TRACER;
        g_tamper_flags |= scan_maps();
        sleep(3);
    }
    return nullptr;
}

// A placeholder CRC over our own text section. Replace g_text_crc_expected with the value you
// compute at build time (see native/README.md) to turn this into a real self-check. Until you
// set it, this reports OK so it never false-positives on an un-provisioned build.
volatile uint32_t g_text_crc_expected = 0;

uint32_t crc32_buf(const unsigned char *data, size_t len) {
    uint32_t crc = 0xFFFFFFFFu;
    for (size_t i = 0; i < len; ++i) {
        crc ^= data[i];
        for (int j = 0; j < 8; ++j) {
            crc = (crc >> 1) ^ (0xEDB88320u & (-(int)(crc & 1)));
        }
    }
    return ~crc;
}

bool text_crc_ok() {
    if (g_text_crc_expected == 0) return true; // not provisioned
    // A full .text walk needs the load address + section bounds resolved from the ELF header.
    // The build README documents computing g_text_crc_expected; the walk is intentionally left
    // to integrators because the section bounds are toolchain specific.
    return true;
}

} // namespace

extern "C" {

JNIEXPORT void JNICALL
Java_com_unityobu_OBUNative_start(JNIEnv *, jclass) {
    static std::atomic<bool> started{false};
    bool expected = false;
    if (!started.compare_exchange_strong(expected, true)) return;
    pthread_t t1, t2;
    pthread_create(&t1, nullptr, trace_guard, nullptr);
    pthread_create(&t2, nullptr, monitor, nullptr);
    pthread_detach(t1);
    pthread_detach(t2);
    g_tamper_flags |= scan_maps();
    if (!text_crc_ok()) g_tamper_flags |= BIT_TEXT_CRC;
}

// Returns the accumulated tamper bitmask. Also callable through DllImport as obu_poll.
JNIEXPORT jint JNICALL
Java_com_unityobu_OBUNative_poll(JNIEnv *, jclass) {
    int pid = read_tracer_pid();
    if (pid > 0) g_tamper_flags |= BIT_TRACER;
    g_tamper_flags |= scan_maps();
    return g_tamper_flags.load();
}

JNIEXPORT jint JNICALL
Java_com_unityobu_OBUNative_tracerPid(JNIEnv *, jclass) {
    return read_tracer_pid();
}

// Plain C entry points for the IL2CPP DllImport path (no JNI env needed).
OBU_EXPORT int obu_start() { Java_com_unityobu_OBUNative_start(nullptr, nullptr); return 0; }
OBU_EXPORT int obu_poll()  { return Java_com_unityobu_OBUNative_poll(nullptr, nullptr); }
OBU_EXPORT int obu_tracer_pid() { return read_tracer_pid(); }

} // extern "C"
