# Quantum Inspector

An editor-only orbital wheel and cluster graph for exploring GameObjects, components, assets, and relationships, with embedded native component inspectors.

## Install from Git

Requires Unity 6000.3.7f1 or newer and Git installed on your computer. In Unity, open **Window > Package Management > Package Manager**, choose **+ > Install package from git URL**, and enter:

```text
https://github.com/GBear9551/QuantumInspector.git?path=/Assets/FightSongStudio/QuantumInspector/Editor#main
```

This URL becomes available after the package changes are committed and pushed to the repository. For reproducible installs, replace `main` with a commit SHA containing this package, or a release tag. Private repositories require Git credentials with repository access.

Open **Tools > Quantum Inspector > Open**. Font scaling and other preferences are under **Tools > Quantum Inspector > Settings**.

The package contains only editor tooling. It does not require URP or the demo project. The existing editor-only assembly definition prevents code from entering player builds.

## Project data

On first use, editable settings and component aliases are created under `Assets/QuantumInspector/Editor/`. These belong to your project and survive package upgrades. Package assets are read-only defaults; changes are never saved into the Git package cache. Existing source-project settings and aliases are retained when the tool lives under Assets.

Do not install the Git package alongside an existing Assets copy of Quantum Inspector: both contain the same scripts and asset GUIDs. Back up settings and aliases, remove the old tool copy, then install the package. This source repository already contains the tool, so it does not need to install its own Git dependency.

## Development

The package root intentionally lives in the existing Editor directory so the same sources work in the development project and through UPM without copying files or changing GUIDs. Demo content remains outside the package. Verification helpers are editor-only development utilities; some require the source project demo fixtures and should not be run in a consuming project.
