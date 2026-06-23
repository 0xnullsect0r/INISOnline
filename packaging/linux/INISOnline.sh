#!/bin/sh
# Launcher for the Flatpak: the Godot .NET export keeps its managed assemblies in a
# data_INISOnline_* directory next to the binary, so run from that directory.
cd /app/bin || exit 1
exec /app/bin/INISOnline.x86_64 "$@"
