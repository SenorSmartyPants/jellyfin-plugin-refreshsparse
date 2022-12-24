<h1 align="center">Jellyfin Refresh Sparse Episodes Plugin</h1>
<h3 align="center">For use in the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
<a href="https://github.com/SenorSmartyPants/jellyfin-plugin-refreshsparse/actions/workflows/build-dotnet.yml">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/actions/workflow/status/SenorSmartyPants/jellyfin-plugin-refreshsparse/build-dotnet.yml">
</a>
<a href="https://github.com/SenorSmartyPants/jellyfin-plugin-refreshsparse">
<img alt="GPLv3 License" src="https://img.shields.io/github/license/SenorSmartyPants/jellyfin-plugin-refreshsparse.svg"/>
</a>
<a href="https://github.com/SenorSmartyPants/jellyfin-plugin-refreshsparse/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/SenorSmartyPants/jellyfin-plugin-refreshsparse.svg"/>
</a>
</p>

## About

This plugin adds a scheduled job to search for and update episodes that are missing certain metadata.

Metadata that are checked:

- Name
    - Is it a date? "January 1, 2022"
    - User supplied list of substrings
- Overview
- Primary Image
- Number of provider IDs

Refresh all metadata/images options.

Pretend option to try it out without updating metadata.

## Installation

Add [this link][1] to "Repositories" in Jellyfin settings.

[1]: https://raw.githubusercontent.com/SenorSmartyPants/jellyfin-plugin-refreshsparse/master/manifest.json

## Build

1. To build this plugin you will need [.Net 5.x](https://dotnet.microsoft.com/download/dotnet/5.0).

2. Build plugin with following command
  ```
  dotnet publish --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/RefreshSparse` folder (you might need to create the folders) of your JF install

## Contributing

We welcome all contributions and pull requests! If you have a larger feature in mind please open an issue so we can discuss the implementation before you start.
In general refer to our [contributing guidelines](https://github.com/jellyfin/.github/blob/master/CONTRIBUTING.md) for further information.

## Licence

This plugins code and packages are distributed under the GPLv3 License. See [LICENSE](./LICENSE) for more information.
