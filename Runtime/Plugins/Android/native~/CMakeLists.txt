cmake_minimum_required(VERSION 3.18.1)
project(obunative)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(obunative SHARED src/obu_native.cpp)

find_library(log-lib log)
target_link_libraries(obunative ${log-lib})

# Strip symbols and harden in release.
target_compile_options(obunative PRIVATE
    -fvisibility=hidden
    -ffunction-sections
    -fdata-sections
    -O2)
target_link_options(obunative PRIVATE
    -Wl,--gc-sections
    -Wl,-z,max-page-size=16384)
