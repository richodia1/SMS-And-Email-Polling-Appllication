 

using System;
using System.IO;

using ICSharpCode.SharpZipLib.Zip;
using System.Threading;

namespace SMSGATE
{
    public class CFileCompressionUtility
    {

        public CFileCompressionUtility()
        {

        }
       
        public bool PKCompress(string sourceFolder,string destinationFolder,log4net.ILog logger)
        {
             
            bool rtn = false;
            
            
            // Perform some simple parameter checking.  More could be done
            // like checking the target file name is ok, disk space, and lots
            // of other things, but for a demo this covers some obvious traps.


            if (!Directory.Exists(sourceFolder))
            {
                 
                return rtn;
            }

            //if (!Directory.Exists(sourceFolder))
            //{

            //    return rtn;
            //}


            try
            {
                // Depending on the directory this could be very large and would require more attention
                // in a commercial package.
                string sptern = "*" + DateTime.Now.Year + "*";
                string[] filenames = Directory.GetFiles(sourceFolder,sptern);
                logger.Info(filenames.Length.ToString() + " ready for compression\n");
                // 'using' statements gaurantee the stream is closed properly which is a big source
                // of problems otherwise.  Its exception safe as well which is great.
                using (ZipOutputStream s = new ZipOutputStream(File.Create(destinationFolder)))
                {

                    s.SetLevel(9); // 0 - store only to 9 - means best compression

                    byte[] buffer = new byte[94096];

                    foreach (string file in filenames)
                    {

                        // Using GetFileName makes the result compatible with XP
                        // as the resulting path is not absolute.
                        ZipEntry entry = new ZipEntry(Path.GetFileName(file));

                        // Setup the entry data as required.

                        // Crc and size are handled by the library for seakable streams
                        // so no need to do them here.

                        // Could also use the last write time or similar for the file.
                        entry.DateTime = DateTime.Now;
                        s.PutNextEntry(entry);

                        using (FileStream fs = File.OpenRead(file))
                        {

                            // Using a fixed size buffer here makes no noticeable difference for output
                            // but keeps a lid on memory usage.
                            int sourceBytes;
                            do
                            {
                                sourceBytes = fs.Read(buffer, 0, buffer.Length);
                                s.Write(buffer, 0, sourceBytes);
                            } while (sourceBytes > 0);
                        }

                        logger.Info("Compressed " + file + "\n");
                       

                    }

                    // Finish/Close arent needed strictly as the using statement does this automatically

                    // Finish is important to ensure trailing information for a Zip file is appended.  Without this
                    // the created file would be invalid.
                    s.Finish();

                    // Close is important to wrap things up and unlock the file.
                    s.Close();
                    rtn = true;

                    Thread.Sleep(100000);
                    logger.Info("Ready to Delete files\n");
                    foreach (string filetodelete in filenames)
                    {
                        File.Delete(filetodelete);
                        logger.Info("Deleted: " + filetodelete);

                    }



                }
            }
            catch (Exception ex)
            {
                logger.Error("Exception during processing {0}\n", ex);

                // No need to rethrow the exception as for our purposes its handled.
            }


            return rtn;
        }
    }
}
