using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    public class FileOpenMonitorInterface : MarshalByRefObject
    {
        public void IsInstalled(Int32 InClientPID)
        {
            System.Diagnostics.Debug.WriteLine("FileMon has been installed in target {0}.", InClientPID);
        }

        public void OnCreateFile(Int32 InClientPID, String[] InFileNames)
        {
            for (int i = 0; i < InFileNames.Length; i++)
            {
                OnFileOpening(null, new FileOpeningEventArg(InFileNames[i], InClientPID));
            }
        }

        public void OnCreateProcess(Int32 InClientPID, String[] InFileNames)
        {
            for (int i = 0; i < InFileNames.Length; i++)
            {
                OnFileOpening(null, new FileOpeningEventArg(InFileNames[i], InClientPID));
            }
        }

        public void ReportException(Int32 InClientPID, Exception InInfo)
        {
            System.Diagnostics.Debug.WriteLine("The target process {0} has reported an error:\r\n" + InInfo.ToString(), InClientPID);
        }

        public void Ping()
        {

        }

        public static event EventHandler<FileOpeningEventArg> OnFileOpening;
    }

    public class FileOpeningEventArg
    {
        public string filename { get; set; }
        public int id { get; set; }

        public FileOpeningEventArg(string filename, int id)
        {
            this.filename = filename;
            this.id = id;
        }
    }
}
