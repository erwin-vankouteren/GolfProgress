# R8 rules for the release build (#118). Retrofit, OkHttp, kotlinx.serialization, Hilt and
# Compose ship their own keep rules, which cover the generated API client and its models;
# trying a minified build: docs/clients/android-release.md → "Checking a release build locally".

# Stack traces in crash reports (#119) keep their line numbers; the mapping file restores names.
-keepattributes SourceFile, LineNumberTable
-renamesourcefileattribute SourceFile
