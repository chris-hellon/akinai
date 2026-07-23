#!/bin/bash
cd "$(dirname "$0")"

npx tailwindcss \
  -i ./wwwroot/css/input.css \
  -o ./wwwroot/css/shared.css \
  --watch