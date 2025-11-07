{
  inputs.nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";
  inputs.arcanelibs.url = "github:TheArcaneBrony/ArcaneLibs";
  inputs.arcanelibs.inputs.nixpkgs.follows = "nixpkgs";

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
      arcanelibs,
    }:
    let
      pkgs = nixpkgs.legacyPackages.x86_64-linux;
      rVersion =
        let
          rev = self.sourceInfo.shortRev or self.sourceInfo.dirtyShortRev;
          date = builtins.substring 0 8 self.sourceInfo.lastModifiedDate;
          time = builtins.substring 8 6 self.sourceInfo.lastModifiedDate;
        in
        "preview.${date}-${time}+${rev}";

      makeNupkg =
        {
          name,
          nugetDeps ? null,
          projectReferences ? [ ],
          projectFile ? "${name}/${name}.csproj",
        }:
        pkgs.buildDotnetModule rec {
          inherit projectReferences nugetDeps;

          pname = "${name}";
          version = "1.0.0-" + rVersion;
          dotnetPackFlags = [
            "--include-symbols"
            "--include-source"
            "--version-suffix ${rVersion}"
          ];
          #          dotnetFlags = [ "-v:diag" ];
          dotnet-sdk = pkgs.dotnet-sdk_10;
          dotnet-runtime = pkgs.dotnet-aspnetcore_10;
          src = ./.;
          projectFile = [
            "${name}/${name}.csproj"
          ];
          packNupkg = true;
          meta = with pkgs.lib; {
            description = "Rory&::LibMatrix";
            homepage = "https://cgit.rory.gay/matrix/LibMatrix.git";
            license = licenses.agpl3Plus;
            maintainers = with maintainers; [ RorySys ];
          };
        };
    in
    {
      packages.x86_64-linux =
        let
          # HACKHACK: trim version string until nuget learns to deal with semver properly
          # See: https://github.com/NuGet/Home/issues/14628
          ArcaneLibs = arcanelibs.packages."${pkgs.stdenv.hostPlatform.system}".ArcaneLibs.overrideAttrs (old: {
            __intentionallyOverridingVersion = true;
            version = builtins.substring 0 29 old.version; # "1.0.0-preview-20251106-123456";
          });
          LibMatrix = self.packages."${pkgs.stdenv.hostPlatform.system}".LibMatrix.overrideAttrs (old: {
            __intentionallyOverridingVersion = true;
            version = builtins.substring 0 29 old.version; # "1.0.0-preview-20251106-123456";
          });
        in
        {
          LibMatrix = makeNupkg {
            name = "LibMatrix";
            nugetDeps = LibMatrix/deps.json;
            projectReferences = [ ArcaneLibs ];
          };
          LibMatrix-EventTypes = makeNupkg {
            name = "LibMatrix.EventTypes";
            projectReferences = [
              ArcaneLibs
              LibMatrix
            ];
          };
          LibMatrix-Federation = makeNupkg {
            name = "LibMatrix.Federation";
            nugetDeps = LibMatrix.Federation/deps.json;
            projectReferences = [
              ArcaneLibs
              LibMatrix
            ];
          };
        };
    };
}
