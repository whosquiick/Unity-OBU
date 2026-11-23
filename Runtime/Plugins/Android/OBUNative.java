package com.unityobu;

// Thin JNI shim so the managed side can reach libobunative.so through AndroidJavaObject when
// the DllImport path isn't preferred. Loads lazily; if the .so isn't shipped, callers catch the
// UnsatisfiedLinkError and fall back to the pure-C# probes.
public final class OBUNative {

    private static boolean loaded = false;

    static {
        try {
            System.loadLibrary("obunative");
            loaded = true;
        } catch (Throwable t) {
            loaded = false;
        }
    }

    private OBUNative() {}

    public static boolean isAvailable() {
        return loaded;
    }

    public static native void start();

    public static native int poll();

    public static native int tracerPid();
}
