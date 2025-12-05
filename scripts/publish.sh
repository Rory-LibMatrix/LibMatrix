nom build .\#nugetArtifactDir.x86_64-linux -j4 -L
for p in ./result/*.nupkg; do
    echo "Pushing $p to NuGet..."
    dotnet nuget push "$p" -k ${NUGET_KEY} --source https://api.nuget.org/v3/index.json --skip-duplicate
done