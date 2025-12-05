{
  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
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
          projectFile ? "${pkgs.lib.replaceString "RoryLibMatrix" "LibMatrix" name}/${pkgs.lib.replaceString "RoryLibMatrix" "LibMatrix" name}.csproj",
        }@args:
        pkgs.buildDotnetModule rec {
          inherit projectReferences nugetDeps projectFile;

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
          src = pkgs.lib.cleanSource ./.;
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
            name = "RoryLibMatrix";
            nugetDeps = LibMatrix/deps.json;
            projectReferences = [ ArcaneLibs ];
          };
          LibMatrix-EventTypes = makeNupkg {
            name = "RoryLibMatrix.EventTypes";
            projectReferences = [
              ArcaneLibs
              #              LibMatrix
            ];
          };
          LibMatrix-Federation = makeNupkg {
            name = "RoryLibMatrix.Federation";
            nugetDeps = LibMatrix.Federation/deps.json;
            projectReferences = [
              ArcaneLibs
              LibMatrix
            ];
          };
          LibMatrix-Bot-Utils = makeNupkg {
            name = "RoryLibMatrix.Utilities.Bot";
            nugetDeps = Utilities/LibMatrix.Utilities.Bot/deps.json;
            projectFile = "Utilities/LibMatrix.Utilities.Bot/LibMatrix.Utilities.Bot.csproj";
            projectReferences = [
              ArcaneLibs
              LibMatrix
            ];
          };
        };
      checks = pkgs.lib.attrsets.unionOfDisjoint {
        # Actual checks
      } self.packages;
      nupkgs.x86_64-linux = pkgs.lib.mapAttrs (
        name: pkg:
        (
          with pkgs;
          pkgs.runCommand (pkg.pname + "-" + pkg.version + ".nupkg") { } ''
            echo 'Creating zip archive for ${name}'
            set -x
            cd "${pkg.out}/share/nuget/packages/${lib.toLower pkg.pname}/${pkg.version}"
            ls -la
            # NuGet doesn't care about compression flags
            ${lib.getExe pkgs.zip} -db -ds 32k -9 -r "$out" *
            set +x
          ''
        )
      ) self.packages.x86_64-linux;
      nugetArtifactDir.x86_64-linux =
        let
          outPaths = pkgs.lib.mapAttrsToList (name: pkg: pkg.out) self.nupkgs.x86_64-linux;
        in
        pkgs.runCommand "nuget-artifacts" { } ''
          mkdir -p $out
          for path in ${pkgs.lib.concatStringsSep " " outPaths}; do
            ln -vs ''\${path} $out/
          done
        '';
    };
}
