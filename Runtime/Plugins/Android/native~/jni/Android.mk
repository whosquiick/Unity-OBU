LOCAL_PATH := $(call my-dir)
include $(CLEAR_VARS)
LOCAL_MODULE    := obunative
LOCAL_SRC_FILES := ../src/obu_native.cpp
LOCAL_CPPFLAGS  := -std=c++14 -fvisibility=hidden -O2 -ffunction-sections -fdata-sections
LOCAL_LDLIBS    := -llog
LOCAL_LDFLAGS   := -Wl,--gc-sections
include $(BUILD_SHARED_LIBRARY)
