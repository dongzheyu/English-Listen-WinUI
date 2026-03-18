cmake_minimum_required(VERSION 3.20)
project(EnglishListenNative VERSION 1.0.0)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Create shared library
add_library(EnglishListenNative SHARED
    QT6DictationBackend.cpp
    NativeDictationExports.cpp
)

# Create test executable
add_executable(TestBackend
    TestBackend.cpp
)

target_link_libraries(TestBackend EnglishListenNative)

# Set output directory and name to match C# expectations
set_target_properties(EnglishListenNative PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    PREFIX ""
    SUFFIX ".dll"
)

# Windows-specific settings
if(WIN32)
    target_compile_definitions(EnglishListenNative PRIVATE _CRT_SECURE_NO_WARNINGS)
    target_link_libraries(EnglishListenNative PRIVATE kernel32 user32)
endif()