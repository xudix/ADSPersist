using System;
using TwinCAT.Ads;
using TwinCAT;
using TwinCAT.TypeSystem;
using TwinCAT.Ads.TypeSystem;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ADSPersist
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            string helpStr = "Arguments: command AmsNetID Port ConfigFile. Examples:\nDacConfigManager.exe read \"127.0.0.1.1.1\" 851 configFile.xml\nDacConfigManager.exe write \"127.0.0.1.1.1\" 852 configFile.xml ";
            if (args.Length < 3)
                Console.WriteLine(helpStr);
            else
            {
                ADSPersist manager;
                switch (args[0].ToLower())
                {
                    case "write":
                        if (args.Length > 3)
                        {
                            manager = new ADSPersist(args[1], int.Parse(args[2]), args[3]);
                            await manager.WriteToPLC();
                        }
                        else
                            Console.WriteLine("config file is required.\n" + helpStr);
                        break;
                    case "read":
                        manager = new ADSPersist(args[1], int.Parse(args[2]), args.Length > 3 ? args[3] : "");
                        await manager.ReadFromPLC();
                        break;
                    default:
                        Console.WriteLine(helpStr);
                        break;
                }
            }
        }
    }

    internal class ADSPersist
    {
        public ADSPersist(string amsNetID, int port, string confFilePath = "")
        {
            AmsNetIDStr = amsNetID;
            Port = port;
            ConfFilePath = confFilePath;
        }

        public async Task ReadFromPLC()
        {
            Console.WriteLine("Reading from PLC...");
            if(await ReadFromTargetAsync(AmsNetIDStr, Port))
            {
                Console.WriteLine("Writing to file...");
                //if(WriteToFile(ConfFilePath))
                if(await WriteToFileAsync(ConfFilePath))
                    Console.WriteLine("Complete!");
                else
                    Console.WriteLine("write to file fail!");
            }
            else
                Console.WriteLine("read from target fail!");

        }

        public async Task WriteToPLC()
        {
            Console.WriteLine("Reading from file...");
            if(await ReadFromFileAsync(ConfFilePath))
            {
                Console.WriteLine("Writing to PLC...");
                if(await WriteToTargetAsync(AmsNetIDStr, Port))
                    Console.WriteLine("Complete!");
                else
                    Console.WriteLine("write to target fail!");
            }
            else
                Console.WriteLine("read from file fail!");
        }

        ~ADSPersist()
        {
            if (adsClient != null)
                adsClient.Dispose();
        }




        private async Task<bool> WriteToFileAsync(string path = "configFile.xml")
        {
            if (path == "")
                path = "configFile.xml";
            try
            {
                await Task.Run(() =>
                {
                    using (var writer = XmlWriter.Create(path, new XmlWriterSettings() { Indent = true }))
                    {
                        XmlSerializer serializer = new XmlSerializer(serializabaleSymbols.GetType());
                        serializer.Serialize(writer, serializabaleSymbols);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private bool WriteToFile(string path = "configFile.xml")
        {
            if (path == "")
                path = "configFile.xml";
            try
            {
                using (var writer = XmlWriter.Create(path, new XmlWriterSettings() { Indent = true }))
                {
                    XmlSerializer serializer = new XmlSerializer(serializabaleSymbols.GetType());
                    serializer.Serialize(writer, serializabaleSymbols);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }


        private async Task<bool> WriteToTargetAsync(string amsNetID, int port)
        {
            try
            {
                using (adsClient = new AdsClient())
                {
                    if(amsNetID.ToLower() == "local")
                        adsClient.Connect(AmsNetId.Local, port);
                    else
                        adsClient.Connect(amsNetID, port);
                    foreach (var symbol in serializabaleSymbols)
                    {
                        await adsClient.WriteValueAsync(symbol.Path, symbol.Value, CancellationToken.None);
                    }
                    return true;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0}\n{1}", ex.Message, ex.StackTrace));
                return false;
            }
        }


        private async Task<bool> ReadFromTargetAsync(string amsNetID, int port)
        {
            try
            {
                using (adsClient = new AdsClient())
                {
                    if (amsNetID.ToLower() == "local")
                        adsClient.Connect(AmsNetId.Local, port);
                    else
                        adsClient.Connect(amsNetID, port);
                    IDynamicSymbolLoader loader = (IDynamicSymbolLoader)SymbolLoaderFactory.Create(adsClient, SymbolLoaderSettings.DefaultDynamic);
                    ResultDynamicSymbols resultSymbols = await loader.GetDynamicSymbolsAsync(CancellationToken.None);
                    if (!resultSymbols.Succeeded)
                        return false;
                    Func<ISymbol, bool> selector = (s) => s.IsPersistent && (s.Category == DataTypeCategory.Primitive || s.Category == DataTypeCategory.String || s.Category == DataTypeCategory.Enum);
                    SymbolIterator iterator = new SymbolIterator(resultSymbols.Symbols, true, selector);
                    //Func<ISymbol, bool> selector2 = (s) => s.IsPersistent;
                    //SymbolIterator iterator2 = new(symbols, true, selector2);

                    foreach (DynamicSymbol symbol in iterator)
                    {
                        if (symbol.Category == DataTypeCategory.Enum)
                            Console.ReadKey();
                        serializabaleSymbols.Add(new SerializableValueSymbol(symbol));
                    }
                    return true;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0}\n{1}", ex.Message, ex.StackTrace));
                return false;
            }
        }

        private async Task<bool> ReadFromFileAsync(string path)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(serializabaleSymbols.GetType());
                await Task.Run(() =>
                {
                    using (TextReader reader = new StreamReader(path))
                    {
                        dynamic result = serializer.Deserialize(reader);
                        if (result != null)
                            serializabaleSymbols = result;
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }


        private List<SerializableValueSymbol> serializabaleSymbols = new List<SerializableValueSymbol>();
        private AdsClient adsClient = new AdsClient();

        public string AmsNetIDStr { get; set; }
        public int Port { get; set; }
        public string ConfFilePath { get; set; }
    }
}
