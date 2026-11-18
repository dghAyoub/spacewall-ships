#!/bin/bash
# login: the bridge always starts; the on/off flag decides whether the animation runs
exec "$(dirname "$(readlink -f "$0")")/start.sh"
