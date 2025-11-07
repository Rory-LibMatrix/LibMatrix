#! /usr/bin/env sh
for p in `nix flake show --json | jq '.packages."x86_64-linux" | keys[]' -r`
do
    nix build .\#${p}.passthru.fetch-deps && ./result ./${p//-/\.}/deps.json
done
