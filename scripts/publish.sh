BASEDIR="$PWD"
rm ./result* *.nupkg

for p in `nix flake show --json | jq '.packages."x86_64-linux" | keys[]' -r`
do
    nix build .\#${p} -j4 -L --out-link ./result-${p//-/\.} &
done
wait

for p in result*/share/nuget/packages/*/*/.unpacked
do
    PNAME=$(basename `realpath "${p}/../.."`)
    PRNAME=$(basename $(cd "${p}/../../../../../.." && echo $PWD))
    echo $PNAME: $PRNAME
    cd "${p}" || continue
    zip -db -ds 32k -9 -r "${BASEDIR}/${PNAME//./-}.nupkg" *
    cd -
    dotnet nuget push *.nupkg -k ${NUGET_KEY} --source https://api.nuget.org/v3/index.json --skip-duplicate
    rm -rfv "${PRNAME}" "${PNAME//./-}.nupkg"
done