#!/usr/bin/env bash
# Builds libobunative.so for the ABIs in jni/Application.mk and copies them into
# ../libs/<abi>/ where Unity expects Android native plugins.
set -euo pipefail

if [ -z "${ANDROID_NDK_HOME:-}" ]; then
  echo "Set ANDROID_NDK_HOME to your NDK (e.g. ~/Library/Android/sdk/ndk/26.1.10909125)"
  exit 1
fi

HERE="$(cd "$(dirname "$0")" && pwd)"
cd "$HERE"

# ndk-build route (simplest):
"$ANDROID_NDK_HOME/ndk-build" NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=jni/Android.mk NDK_APPLICATION_MK=jni/Application.mk

mkdir -p ../libs/arm64-v8a ../libs/armeabi-v7a
cp -f libs/arm64-v8a/libobunative.so   ../libs/arm64-v8a/   2>/dev/null || true
cp -f libs/armeabi-v7a/libobunative.so ../libs/armeabi-v7a/ 2>/dev/null || true

echo "Done. Copied .so into Runtime/Plugins/Android/libs/<abi>/"
echo "In Unity, select each .so and set Platform=Android, CPU=ARM64 / ARMv7 in the import settings."
