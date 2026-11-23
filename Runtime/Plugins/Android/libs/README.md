# Android native libraries

Build `libobunative.so` from `../native~/` and drop the per-ABI outputs here:

```
libs/arm64-v8a/libobunative.so
libs/armeabi-v7a/libobunative.so
```

Select each `.so` in Unity and set Platform = Android, CPU = ARM64 / ARMv7. If this folder is
empty, Unity OBU still works — it falls back to the pure-C# probes.
