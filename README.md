GitVersionTree
--------------

A cross-platform desktop tool (now powered by [Avalonia UI](https://avaloniaui.net/)) to help visualize git revisions and branches on Windows, macOS, and Linux.

Introduction
------------

(First of all, sorry for my english.)

Coming from Clearcase background, I find it difficult and confusing sometime to understand and visualize my project development in git when it grows with many branch.

Using `gitk` or `git log --oneline --color --all --decorate --graph`, I couldn't understand the relationship between each branches. Eg. where this branch come out from and where it merged to etc. Sometime the branch just appear out of nowhere because there is a new commit in a branch. I find it hard to also to follow my master branch inside the 'branch forest' because it won't grow in straight line. It would be more severe when I started out git.

Therefore, I made this little tool to help me solve my problem. To whoever face this problem also, I hope this tool can help you.

Screenshot
----------

Screenshot of Gitk. Branch may not be in straight line.

![Screenshot of Gitk](https://raw.github.com/crc8/GitVersionTree/master/sample_gitk.png)

Screenshot of GitVersionTree. Keeping branch in their own line. From oldest  commit at the top down to latest at the bottom.

![Screenshot of GitVersionTree](https://raw.github.com/crc8/GitVersionTree/master/sample_gvt.png)

Features
--------

- Tree is from top to bottom
- Same branch will be in same straight line tree
- Date is sort on the branch itself instead with all branch
- Tag and branch name on the side
- Easy scrolling / zooming with PDF

Project Status
--------------

- I would say this project still in beta.
- Contructive feedback are always welcome.
- Good to handle git project with less than 500 commits. Should be able to visualize clearly.
- Tested with jquery project with 5000+ commits. Although the tool still can generate the version tree, but you wouldn't want to go thru each commits, only to visualize some development of the important branch only.
- Will generate out a `.pdf` file, a `.dot` file and a `.ps` file. So you can either view PDF with PDF reader, or Dot file with zgrviewer, or Postscript file with Evince.

Requirement
-----------

This tool requires:

- [Git](http://git-scm.com/)
- [Graphviz](http://www.graphviz.org/) (only the `dot` executable is needed)
- [.NET 8 SDK](https://dotnet.microsoft.com/) or newer
- Any PDF reader/viewer
- A Postscript viewer (optional)

The app now targets .NET 8 with Avalonia so it can run on Windows, macOS, and Linux. Use `dotnet build` (or your IDE) to restore the NuGet packages and run the cross-platform UI.

Building
--------

- Run `dotnet build GitVersionTree.sln` (or use your IDE) to compile/run on any desktop platform.
- To package a macOS `.app` bundle, execute `./scripts/build-macos-app.sh [osx-arm64|osx-x64|osx-universal|osx-all]`. The script publishes a self-contained build, converts `GitVersionTree/main.ico` into the proper `.icns` bundle icon (requires macOS `sips`, Python 3, and `lipo` when targeting `osx-universal`), assembles `dist/macos-<rid>/GitVersionTree.app`, and creates a ready-to-distribute `GitVersionTree_macos-<rid>.zip`. Use `osx-all` to generate every variant in one go. Running `osx-universal` directly cleans up the intermediate arm64/x64 publish directories once the universal bundle is produced (they are preserved when `osx-all` orchestrates the build).

Pending
-------

- Output enhancement (Colors, Node alignment, etc) 
- Background process
- Max count selection
- Date range selection
- Integrated Git (?)
- Integrated Graphviz (?)
- Integrated PDF Viewer (?)

Download
--------

Latest release:
[v1.0](https://github.com/crc8/GitVersionTree/releases)

Changelog
---------

- v1.0 - 17 August 2013

	- Initial baseline

License
-------

[GPL V2](https://raw.github.com/crc8/GitVersionTree/master/LICENSE)

Disclaimer
----------

The developers of this software provide the software "as is," and you use the software at your own risk. The developers make no warranties as to performance, merchantability, fitness for a particular purpose, or any other warranties whether expressed or implied. No oral or written communication from or information provided by the developers shall create a warranty. Under no circumstances shall the developers be liable for direct, indirect, special, incidental, or consequential damages resulting from the use, misuse, or inability to use this software, even if the developers has been advised of the possibility of such damages.
