#!/usr/bin/env bash
# uso: bash render.sh <nome> [vistas] [escala]   -> previa/<nome>_<vista>.png
AQUI="D:/unity/CSManager/Tools/brasa/v10"
"/d/SteamLibrary/steamapps/common/Blender/blender.exe" -b --factory-startup --python "$AQUI/brasa10_blender.py" -- "$AQUI/previa/$1" "${2:-topo,jogo,tr,ct,trans,a,b,meio}" "${3:-1.0}" 2>&1 | grep -E "^(casas|fileiras|paredes|objetos|ok)|Error|Traceback|File \"|line [0-9]+" | tail -25
