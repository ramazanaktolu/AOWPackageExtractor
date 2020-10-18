using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Extractor
{
    class Program
    {
        static bool list = false;
        static string input = "";
        static string output = "";
        static bool helpRequest = false;
        static bool extract = true;
        static void Main(string[] args)
        {
            parseARgs(args);
            if (version)
            {
                Console.WriteLine("{0}",
            typeof(Program).Assembly.GetName().Version);
                return;
            }
            if (helpRequest)
            {
                help();
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                help();
                return;
            }
            if (string.IsNullOrWhiteSpace(output) && Path.GetExtension(input).IndexOf(".lys", StringComparison.InvariantCultureIgnoreCase) == -1)
            {
                list = true;
            }
            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"File not found: {input}");
                return;
                //throw new Exception($"File not found: {input}");
            }
            extract = Path.GetExtension(input).IndexOf(".lys", StringComparison.InvariantCultureIgnoreCase) == -1;
            if (!extract)
            {
                var lines = File.ReadAllLines(input);
                List<FileDetail> filedetails = new List<FileDetail>();
                
                string inputDirectory = Path.GetFullPath(Path.GetDirectoryName(input)) + "\\";
                var maindirectory = Directory.GetDirectories(inputDirectory, "*", SearchOption.TopDirectoryOnly);
                var allfiles = Directory.GetFiles(maindirectory[0], "*.*", SearchOption.AllDirectories);
                foreach (var file in allfiles)
                {
                    FileDetail fd = new FileDetail();
                    fd.Filename = file.Replace(inputDirectory, "");
                    FileInfo fi = new FileInfo(file);
                    fd.Size = (int)fi.Length;
                    fd.Timestamp = new byte[] { 0xDC, 0x07, 0x07, 0x1F, 0x06, 0x38, 0x23, 0x00, 0x00 };
                    filedetails.Add(fd);
                }
                foreach(var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) && line.Contains("="))
                    {
                        var array = line.Split('=');
                        var fn = array[0].Trim();
                        var detail = filedetails.FirstOrDefault(x => x.Filename == fn);
                        if (detail != null)
                        {
                            detail.Timestamp = array[1].Trim().Split(' ').Select(s => Convert.ToByte(s.Trim(), 16)).ToArray();
                        }
                    }
                }
                var tmpPath = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString() + "\\";
                Directory.CreateDirectory(tmpPath);
                try
                {
                    int nameattributesize = 28;//28
                    int headersize = 19;//basic header length, like file identity, file count, header size
                    int totalFileSize = 0;
                    foreach (var file in filedetails)
                    {
                        headersize += nameattributesize + file.Filename.Length; //27 bytes is file attributes
                        //byte[] compressed = Compress(File.ReadAllBytes(inputDirectory + file.Filename));
                        var path = Path.GetDirectoryName(tmpPath + file.Filename);
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        Console.WriteLine($"Compressing file: {Path.GetFileName(file.Filename)}");
                        int packedSize = Compress(inputDirectory + file.Filename, tmpPath + file.Filename);
                        file.PackedSize = packedSize;
                        totalFileSize += packedSize;
                    }
                    totalFileSize += headersize;
                    int offsetPosition = headersize;
                    string savefilename = new DirectoryInfo(inputDirectory).Name.Replace(".files", "");
                    using (var fs = new FileStream(inputDirectory + savefilename, FileMode.Create))
                    {
                        Console.WriteLine("Writing file headers");
                        var id = new byte[] { 0x50, 0x43, 0x4B, 0x30, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00 }; //PCK0
                        byte[] filecount = BitConverter.GetBytes(filedetails.Count);
                        byte[] headsize = BitConverter.GetBytes(headersize);
                        byte[] zero = new byte[] { 0 };

                        fs.Write(id, 0, id.Length);
                        fs.Write(filecount, 0, filecount.Length);
                        fs.Write(headsize, 0, headsize.Length);
                        fs.Write(zero, 0, zero.Length);
                        foreach (var file in filedetails)
                        {
                            var namesize = BitConverter.GetBytes((short)(file.Filename.Length + nameattributesize));
                            byte[] offset = BitConverter.GetBytes(offsetPosition);
                            file.Offset = offsetPosition;
                            offsetPosition += file.PackedSize;
                            byte[] zero4 = new byte[] { 0, 0, 0, 0 };
                            byte[] size = BitConverter.GetBytes(file.Size);
                            byte[] packedSize = BitConverter.GetBytes(file.PackedSize);
                            byte[] timestamp = file.Timestamp;
                            byte[] filename = encoding.GetBytes(file.Filename);
                            fs.Write(namesize, 0, namesize.Length);
                            fs.Write(offset, 0, offset.Length);
                            fs.Write(zero4, 0, zero4.Length);
                            fs.Write(size, 0, size.Length);
                            fs.Write(packedSize, 0, packedSize.Length);
                            fs.Write(timestamp, 0, timestamp.Length);
                            fs.Write(filename, 0, filename.Length);
                            fs.Write(zero, 0, zero.Length);
                        }
                        foreach (var file in filedetails)
                        {
                            Console.WriteLine(file.ToString());
                            using (var tmpFS = new FileStream(tmpPath + file.Filename, FileMode.Open))
                            {
                                fs.Position = file.Offset;
                                tmpFS.CopyTo(fs);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                   
                }
                finally
                {
                    Directory.Delete(tmpPath, true);
                }
                
                


            }
            else
            {
                FileStream filestream = new FileStream(input, FileMode.Open);

                var files = GetFiles(ref filestream);
                if (!list)
                {

                    //extract
                    var fn = Path.GetFileName(input);
                    var directoryname = $"{fn}.files\\";
                    var fullDirectory = Path.Combine(output, directoryname);
                    Directory.CreateDirectory(fullDirectory);
                    string timestamps = "";
                    foreach (var file in files)
                    {
                        bool searched = IsSearched(file);
                        if (!searched)
                        {
                            continue;
                        }
                        Console.WriteLine($"Extracting: {file.Filename}");
                        timestamps += file.Filename + " = " + string.Join(" ", file.Timestamp.Select(s => s.ToString("X2"))) + Environment.NewLine;
                        filestream.Position = file.Offset;
                        var fulllFileName = Path.Combine(fullDirectory, file.Filename);
                        Directory.CreateDirectory(Path.GetDirectoryName(fulllFileName));
                        //Decompress(ReadAsType<byte[]>(ref filestream, file.PackedSize), fulllFileName);
                        var decompress = Decompress(ReadAsType<byte[]>(ref filestream, file.PackedSize));
                        byte[] b = new byte[decompress.Length];
                        var data = decompress.Read(b, 0, b.Length);
                        decompress.Close();
                        decompress.Dispose();

                        File.WriteAllBytes(fulllFileName, b);
                    }
                    File.WriteAllText(Path.Combine(fullDirectory, "items.lys"), timestamps);

                }
                filestream.Close();
                filestream.Dispose();
            }
        }


        static List<FileDetail> GetFiles(ref FileStream fs)
        {
            List<FileDetail> files = new List<FileDetail>();

            var id = ReadAsType<string>(ref fs,4);

            var namesize = ReadAsType<short>(ref fs);
            var zeros = ReadAsType<int>(ref fs);
            var filecount = ReadAsType<int>(ref fs);
            var headsize = ReadAsType<int>(ref fs);
            var tmp = ReadAsType<byte[]>(ref fs, 1);
            for (int i = 0; i < filecount; i++)
            {
                namesize = ReadAsType<short>(ref fs);
                int address = ReadAsType<int>(ref fs);
                zeros = ReadAsType<int>(ref fs);
                int filesize = ReadAsType<int>(ref fs);
                int compressSize = ReadAsType<int>(ref fs);
                byte[] timestamp = ReadAsType<byte[]>(ref fs, 9);
                string name = ReadAsType<string>(ref fs, namesize - 28);
                tmp = ReadAsType<byte[]>(ref fs, 1);
                FileDetail fd = new FileDetail();
                fd.Filename = name;
                if (fd.Filename.Contains('\0'))
                {
                    var splitted = fd.Filename.Split('\0'); //for the .patch files. Patch files have packed file + extract location. so using 2 paths splitted by \0
                    fd.Filename = splitted[0];
                    fd.PatchFilename = splitted[1];
                }
                fd.Offset = address;
                fd.PackedSize = compressSize;
                fd.Size = filesize;
                fd.Timestamp = timestamp;
                files.Add(fd);
                if (list)
                {
                    if (IsSearched(fd))
                    {
                        Console.WriteLine(fd.ToString());
                    }
                    
                }
            }
            return files;
        }

        static bool IsSearched(FileDetail fd)
        {
            if (searchList.Count() > 0)
            {
                if (!searchList.Any(a => fd.Filename.IndexOf(a, StringComparison.OrdinalIgnoreCase) != -1))
                {
                    return false;
                }

            }
            if (regexList.Count() > 0)
            {
                bool proceed = false;
                foreach (var item in regexList)
                {
                    var regex = new Regex(item);
                    if (regex.IsMatch(fd.Filename))
                    {
                        proceed = true;
                    }
                }
                return proceed;
            }
            return true;
        }

        static Encoding encoding = Encoding.GetEncoding("windows-1254");
        static T ReadAsType<T>(ref FileStream fs, int strlen = 1)
        {
            if (typeof(T) == typeof(string))
            {
                byte[] buffer = new byte[strlen];
                fs.Read(buffer, 0, strlen);
                return (T)(object)encoding.GetString(buffer);
            }
            else if (typeof(T) == typeof(byte[]))
            {
                byte[] buffer = new byte[strlen];
                fs.Read(buffer, 0, strlen);
                return (T)(object)buffer;
            }
            else
            {
                var len = Marshal.SizeOf(typeof(T));
                byte[] buffer = new byte[len];
                fs.Read(buffer, 0, len);
                if (len == 2)
                {
                    return (T)(object)BitConverter.ToInt16(buffer, 0);
                }
                    return (T)(object)BitConverter.ToInt32(buffer, 0);
                
            }
        }



        static List<string> searchList = new List<string>();
        static List<string> regexList = new List<string>();
        static void parseARgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                helpRequest = true;
                return;
            }
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if ((arg.Equals("-i", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-input", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    input = args[i + 1];
                    i++;
                }
                else if ((arg.Equals("-c", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-codepage", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    var enc = args[i + 1];
                    encoding = Encoding.GetEncoding(enc);
                    i++;
                }
                else if ((arg.Equals("-s", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-search", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    var search = args[i + 1];
                    if (!searchList.Any(s => s.Equals(search, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        searchList.Add(search);
                    }
                    i++;
                }
                else if ((arg.Equals("-r", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-regex", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    var search = args[i + 1];
                    if (!regexList.Any(s => s.Equals(search, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        regexList.Add(search);
                    }
                    i++;
                }
                else if ((arg.Equals("-cl", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-codepagelist", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    Console.WriteLine("Supported codepages:");
                    foreach (var enc in Encoding.GetEncodings())
                    {
                        Console.WriteLine($"\t{enc.GetEncoding().HeaderName}");
                    }
                    return;
                }
                else if ((arg.Equals("-o", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-output", StringComparison.InvariantCultureIgnoreCase)) && args.Length >= i)
                {
                    output = args[i + 1];
                    i++;
                }
                else if ((arg.Equals("-v", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-version", StringComparison.InvariantCultureIgnoreCase)))
                {
                    version = true;
                }
                else if ((arg.Equals("-h", StringComparison.InvariantCultureIgnoreCase) || arg.Equals("-help", StringComparison.InvariantCultureIgnoreCase)))
                {
                    helpRequest = true;
                }
            }
        }
        static bool version = false;

        static void help()
        {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("\t{0,-20}{1}",
                "-i,-input:", "input path to .package file to export or .lys file to repack.");
            Console.WriteLine("\t{0,-20}{1}", 
                "", "use \" (double quote) before and end of path if path contains space.");

            Console.WriteLine("\t{0,-20}{1}", 
                "-o,-output:", "output directory to export package file.");
            Console.WriteLine("\t{0,-20}{1}", 
                "-c,-codepage:", $"codepage for filenames. default codepage: {encoding.HeaderName}");
            Console.WriteLine("\t{0,-20}{1}", 
                "-cl,-codepagelist:", "prints codepage list supported by your system");
            Console.WriteLine("\t{0,-20}{1}",
                "-s,-search:", "searchs in file name and shows/extracts files which matched.");
            Console.WriteLine("\t{0,-20}{1}",
                "", "this, can be used multiple times");
            Console.WriteLine("\t{0,-20}{1}",
                "-r,-regex:", "searchs in file name with regular expression patterns and shows/extracts");
            Console.WriteLine("\t{0,-20}{1}",
                "", "files which matched. this, can be used multiple times");
            Console.WriteLine("\t{0,-20}{1}", 
                "-v,-version:", "this program's version");
            Console.WriteLine("\t{0,-20}{1}", 
                "-h,-help:", "shows this help message.");
        }

        public static Stream Decompress(byte[] data)
        {
            var outputStream = new MemoryStream();
            using (var compressedStream = new MemoryStream(data))
            using (var inputStream = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(compressedStream, new ICSharpCode.SharpZipLib.Zip.Compression.Inflater(false)))
            {
                inputStream.CopyTo(outputStream);
                outputStream.Position = 0;
                return outputStream;
            }
        }


        static int Compress(string file, string zipFile)
        {
            //var fstowrite = new FileStream(zipFile, FileMode.Create, FileAccess.Write);
            //System.IO.Compression.DeflateStream ds = new System.IO.Compression.DeflateStream(fstowrite, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);
            //using (var ss = new FileStream(file, FileMode.Open, FileAccess.Read))
            //{
            //    ss.CopyTo(ds);
            //    //Console.WriteLine();
            //    //len2 = (int)ds.Length;
            //}

            //ds.Close();
            //ds.Dispose();
            //var len = fstowrite.Length;
            //fstowrite.Close();
            //fstowrite.Dispose();
            //int len2 = (int)len;
            //return len2;

            long length = -1;
            FileStream ifs = null;
            FileStream ofs = null;
            try
            {
                ifs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                ofs = File.Open(zipFile, FileMode.Create, FileAccess.Write, FileShare.None);
                DeflaterOutputStream dos = new DeflaterOutputStream(ofs, new ICSharpCode.SharpZipLib.Zip.Compression.Deflater(ICSharpCode.SharpZipLib.Zip.Compression.Deflater.DEFAULT_COMPRESSION, false));
                byte[] buff = new byte[ifs.Length];
                while (true)
                {
                    int r = ifs.Read(buff, 0, buff.Length);
                    if (r <= 0) break;
                    dos.Write(buff, 0, r);
                }
                dos.Flush();
                dos.Finish();
                length = dos.Length;
                dos.Close();
            }
            finally
            {
                if (ifs != null) ifs.Close();
                if (ofs != null) ofs.Close();
            }
            return (int)length;
        }

        static int Compress2(string file, string zipFile)
        {

            long length = -1;
            FileStream ifs = null;
            FileStream ofs = null;
            try
            {
                ifs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                ofs = File.Open(zipFile, FileMode.Create, FileAccess.Write, FileShare.None);
                DeflaterOutputStream dos = new DeflaterOutputStream(ofs, new ICSharpCode.SharpZipLib.Zip.Compression.Deflater(ICSharpCode.SharpZipLib.Zip.Compression.Deflater.DEFLATED, false));
                byte[] buff = new byte[ifs.Length];
                while (true)
                {
                    int r = ifs.Read(buff, 0, buff.Length);
                    if (r <= 0) break;
                    dos.Write(buff, 0, r);
                }
                dos.Flush();
                dos.Finish();
                length = dos.Length;
                dos.Close();
            }
            finally
            {
                if (ifs != null) ifs.Close();
                if (ofs != null) ofs.Close();
            }
            return (int)length;
        }

        public class FileDetail
        {
            public string Filename { get; set; }
            public byte[] Timestamp { get; set; }
            public int Size { get; set; }
            public int PackedSize { get; set; }
            public int Offset { get; set; }
            public string PatchFilename { get; set; }
            /// <summary>
            /// return format is filename(tab)offset(tab)size(tab)packedsize
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return $"{Filename}\t{Offset}\t{Size}\t{PackedSize}";
            }
        }
    }
}
