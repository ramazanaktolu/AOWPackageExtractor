# AOWPackageExtractor
Age of wushu package file extract and repack

	Usage: Extractor.exe [options]
	Options:
		-i,-input:          input path to .package file to export or .lys file to repack.
				    use " (double quote) before and end of path if path contains space.
		-o,-output:         output directory to export package file.
		-c,-codepage:       codepage for filenames. default codepage: windows-1254
		-cl,-codepagelist:  prints codepage list supported by your system
		-v,-version:        this program's version
		-h,-help:           shows help message.


**Parameters**
* -i, -input: File full path to extract or repack. Supported extensions are .package, .patch and .lys for this program's repack extension. For valid path you should use " (double quotes) at beginning and at the and of path to avoid of reading wrong path. if path contains spaces you MUST to use "(double quotes).

* -o, -output: Directory path for the extraction of files without filename just directory path. Valid path rules are as same as -i, -input for this.

* -c, -codepage: Determine a correct codepage for filenames. When you have Chinese characters inside the package, you should use a codepage that also supports Chinese characters. otherwise file names will be corrupted after extraction and you will have problem with repack them. Default value is "windows-1254" (Turkish) codepage for this parameter, it supports a lot of character sets.

* -cl, -codepagelist: Prints the list of codepages which your system are supported and you can use them.

* -v, -version: This program's current build version. You can check that and you can compare if there is new updates.

* -h, -help: Prints short version of this.

**Usage hints**
1. You can use -i parameter to just print list of files in package. If no -o, -output parameter entered, it will just print files and will not extract them. But it is not same when repacking. repacking doesn't use -o parameter.

1. When printing or extracting files, it will be formatted output with pattern in order "**filename**(tab)**offset**(tab)**filesize**(tab)**packedsize**". They are seperated by Tab
* * **filename**: relative path to file
* * **offset**: start offset of packed file in the package (informational)
* * **filesize**: File size after extract
* * **packedsize**: reduced size in package file

**Example usage**

Open command prompt (cmd), if requires, run it as Administrator. Make sure cmd path is matches with Extractor.exe's directory

	Extractor -i "D:\Age of wushu\res\text.package" -o "D:\Extracts\"
