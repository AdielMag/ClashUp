#!/usr/bin/env python3
"""Generate a Unity-style GUID (32 hex chars, no dashes, lowercase)."""
import uuid
import sys

count = int(sys.argv[1]) if len(sys.argv) > 1 else 1
for _ in range(count):
    print(uuid.uuid4().hex)
