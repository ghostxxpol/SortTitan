#!/usr/bin/env sh
set -eu

mode="${1:-}"

if [ "$mode" = "sorter" ]; then
  shift
  exec dotnet /app/sorter/SortTitan.Sorter.dll "$@"
fi

if [ "$mode" = "generator" ]; then
  shift
  exec dotnet /app/generator/SortTitan.Generator.dll "$@"
fi

echo "Usage:"
echo "  sorter --input <path> --output <path> [options]"
echo "  generator --output <path> --size <bytes> [options]"
exit 2
