using System;
using System.Collections.Generic;
using System.Text;

using System.Runtime.InteropServices;
using System.Security;
using System.Collections;
using System.Net;

namespace Tsukikage.Net
{
    /// <summary>
    /// UPnPを使って、いわゆる「ルータのポート穴あけ」をしてくれるクラス。
    /// </summary>
    public class UPnPWanService
    {

#if BUILD_EXAMPLE
        /// <summary>
        /// テスト兼サンプル
        /// </summary>
        public static void Test()
        {
            Console.WriteLine("探索中...");
            UPnPWanService napt = UPnPWanService.FindUPnPWanService();

            if (napt == null)
            {
                Console.WriteLine("みつからぬ");
                return;
            }

            IPAddress extIP = napt.GetExternalIPAddress();
            IPAddress myIP = napt.GetLocalIPAddress();

            Console.WriteLine("現在のポートマッピングs");
            foreach (var e in napt.GetGenericPortMappingEntries())
                Console.WriteLine("[{0}] {1}:{2} -> {3}:{4} : {5}", e.Protocol, extIP, e.ExternalPort, e.InternalClient, e.InternalPort, e.PortMappingDescription);

            Console.WriteLine("ポートマッピング追加。");
            napt.AddPortMapping(null, 12345, "TCP", 65432, myIP, true, "Test Port mapping", 0);

            Console.WriteLine("現在のポートマッピングs");
            foreach (var e in napt.GetGenericPortMappingEntries())
                Console.WriteLine("[{0}] {1}:{2} -> {3}:{4} : {5}", e.Protocol, extIP, e.ExternalPort, e.InternalClient, e.InternalPort, e.PortMappingDescription);

            Console.WriteLine("ポートマッピング削除。");
            napt.DeletePortMapping(null, 12345, "TCP");

            Console.WriteLine("現在のポートマッピングs");
            foreach (var e in napt.GetGenericPortMappingEntries())
                Console.WriteLine("[{0}] {1}:{2} -> {3}:{4} : {5}", e.Protocol, extIP, e.ExternalPort, e.InternalClient, e.InternalPort, e.PortMappingDescription);
        }
#endif

        /// <summary>
        /// ネットワーク内の UPnP Wan サービスを探す。ちょっと時間がかかる。
        /// 見つからなかったときはnullが返るので気をつける。
        /// </summary>
        /// <returns></returns>
        public static UPnPWanService FindUPnPWanService()
        {
            Guid UPnPDeviceFinderClass = new Guid("E2085F28-FEB7-404A-B8E7-E659BDEAAA02");
            IUPnPDeviceFinder finder = Activator.CreateInstance(Type.GetTypeFromCLSID(UPnPDeviceFinderClass)) as IUPnPDeviceFinder;

            string[] deviceTypes = { "urn:schemas-upnp-org:service:WANPPPConnection:1", "urn:schemas-upnp-org:service:WANIPConnection:1", };
            string[] serviceNames = { "urn:upnp-org:serviceId:WANPPPConn1", "urn:upnp-org:serviceId:WANIPConn1", };
            foreach (string deviceType in deviceTypes)
                foreach (IUPnPDevice device in finder.FindByType(deviceType, 0))
                    foreach (string serviceName in serviceNames)
                        foreach (IUPnPService service in device.Services)
                            return new UPnPWanService() { service = service };
            return null;
        }

        IUPnPService service;
        private UPnPWanService() { }

        static void ThrowForHR(string action, HRESULT_UPnP hr, object result)
        {
            if (hr != HRESULT_UPnP.S_OK)
                throw new COMException("Action " + action + " returns " + hr + " " + result, (int)hr);
        }

        /// <summary>
        /// ポートマッピングを追加する。
        /// </summary>
        /// <param name="remoteHost">通信相手。通信先を限定する場合に指定。</param>
        /// <param name="externalPort">グローバルポート番号。</param>
        /// <param name="protocol">プロトコル名。"TCP" or "UDP"を指定。</param>
        /// <param name="internalPort">内部クライアントのポート番号。</param>
        /// <param name="internalClient">内部クライアントのIPアドレス。</param>
        /// <param name="description">説明。任意。</param>
        /// <param name="leaseDuration">リース期間(秒単位)。0を指定すると無期限</param>
        public void AddPortMapping(string remoteHost, ushort externalPort, string protocol, ushort internalPort, IPAddress internalClient, bool enabled, string description, uint leaseDuration)
        {
            if (string.IsNullOrEmpty(remoteHost)) remoteHost = "";
            if (string.IsNullOrEmpty(description)) description = "";
            if (protocol != "TCP" && protocol != "UDP") throw new ArgumentException("protocol must be \"TCP\" or \"UDP\"", protocol);

            object outArgs = null;
            object result = null;
            HRESULT_UPnP hresult = service.InvokeAction("AddPortMapping",
                new object[] { remoteHost, externalPort, protocol, internalPort, internalClient.ToString(), enabled, description, leaseDuration },
                ref outArgs, out result);
            ThrowForHR("AddPortMapping", hresult, result);
        }

        /// <summary>
        /// ポートマッピングを追加する。
        /// </summary>
        /// <param name="portMapping">追加するポートマッピング</param>
        public void AddPortMapping(UPnPPortMapping portMapping)
        {
            AddPortMapping(portMapping.RemoteHost, portMapping.ExternalPort, portMapping.Protocol, portMapping.InternalPort,
                portMapping.InternalClient, portMapping.Enabled, portMapping.PortMappingDescription, portMapping.LeaseDuration);
        }

        /// <summary>
        /// ポートマッピングを削除する。
        /// </summary>
        /// <param name="remoteHost">追加時に指定した通信相手。</param>
        /// <param name="externalPort">追加時に指定した外部ポート番号。</param>
        /// <param name="protocol">追加時に指定されたプロトコル。</param>
        public void DeletePortMapping(string remoteHost, ushort externalPort, string protocol)
        {
            if (string.IsNullOrEmpty(remoteHost)) remoteHost = "";
            if (protocol != "TCP" && protocol != "UDP") throw new ArgumentException("protocol must be \"TCP\" or \"UDP\"", protocol);

            object outArgs = null;
            object result = null;
            HRESULT_UPnP hresult = service.InvokeAction("DeletePortMapping", new object[] { remoteHost, externalPort, protocol }, ref outArgs, out result);
            ThrowForHR("DeletePortMapping", hresult, result);
        }

        /// <summary>
        /// ポートマッピングを削除する。
        /// </summary>
        /// <param name="portMapping">削除するポートマッピング。RemoteHostとExternalPortとProtocolだけが使われる。</param>
        public void DeletePortMapping(UPnPPortMapping portMapping)
        {
            DeletePortMapping(portMapping.RemoteHost, portMapping.ExternalPort, portMapping.Protocol);
        }

        /// <summary>
        /// 現在設定されているポートマッピング情報を得る。
        /// </summary>
        /// <returns>ポートマッピング情報</returns>
        public List<UPnPPortMapping> GetGenericPortMappingEntries()
        {
            object outArgs = null;
            object result = null;
            List<UPnPPortMapping> list = new List<UPnPPortMapping>(16);
            for (int i = 0; ; i++)
            {
                HRESULT_UPnP hresult = service.InvokeAction("GetGenericPortMappingEntry", new object[] { i }, ref outArgs, out result);
                if (hresult != HRESULT_UPnP.S_OK) break;

                object[] array = (object[])outArgs;
                list.Add(new UPnPPortMapping
                {
                    RemoteHost = (string)array[0],
                    ExternalPort = (ushort)array[1],
                    Protocol = (string)array[2],
                    InternalPort = (ushort)array[3],
                    InternalClient = IPAddress.Parse((string)array[4]),
                    Enabled = (bool)array[5],
                    PortMappingDescription = (string)array[6],
                    LeaseDuration = (uint)array[7],
                });
            }
            return list;
        }

        /// <summary>
        /// グローバルIPアドレスを得る。
        /// </summary>
        /// <returns>IPアドレス</returns>
        public System.Net.IPAddress GetExternalIPAddress()
        {
            object outArgs = null;
            object result = null;
            HRESULT_UPnP hresult = service.InvokeAction("GetExternalIPAddress", new object[] { }, ref outArgs, out result);
            ThrowForHR("GetExternalIPAddress", hresult, result);
            return IPAddress.Parse((string)((object[])outArgs)[0]);
        }

        /// <summary>
        /// 自分のIPアドレス(v4)を得る。
        /// </summary>
        /// <returns>IPアドレス</returns>
        public System.Net.IPAddress GetLocalIPAddress()
        {
            IPAddress[] address = Array.FindAll(Dns.GetHostAddresses(Dns.GetHostName()), 
                a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (address.Length > 0) return address[0];
            return null;
        }

        #region COM Interop
        [ComImport]
        [Guid("ADDA3D55-6F72-4319-BFF9-18600A539B10")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        [SuppressUnmanagedCodeSecurity]
        interface IUPnPDeviceFinder
        {
            [DispId(1610744809)]
            IUPnPDevices FindByType(string bstrTypeURI, int dwFlags);
            [DispId(1610744812)]
            int CreateAsyncFind(string bstrTypeURI, int dwFlags, object punkDeviceFinderCallback);
            [DispId(1610744813)]
            void StartAsyncFind(int lFindData);
            [DispId(1610744814)]
            void CancelAsyncFind(int lFindData);
            [DispId(1610744811)]
            IUPnPDevice FindByUDN(string bstrUDN);
        }

        [ComImport]
        [Guid("3D44D0D1-98C9-4889-ACD1-F9D674BF2221")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        [SuppressUnmanagedCodeSecurity]
        interface IUPnPDevice
        {
            [DispId(1610747809)]
            bool IsRootDevice { get; }
            [DispId(1610747810)]
            IUPnPDevice RootDevice { get; }
            [DispId(1610747811)]
            IUPnPDevice ParentDevice { get; }
            [DispId(1610747812)]
            bool HasChildren { get; }
            [DispId(1610747813)]
            IUPnPDevices Children { get; }
            [DispId(1610747814)]
            string UniqueDeviceName { get; }
            [DispId(1610747815)]
            string FriendlyName { get; }
            [DispId(1610747816)]
            string Type { get; }
            [DispId(1610747817)]
            string PresentationURL { get; }
            [DispId(1610747818)]
            string ManufacturerName { get; }
            [DispId(1610747819)]
            string ManufacturerURL { get; }
            [DispId(1610747820)]
            string ModelName { get; }
            [DispId(1610747821)]
            string ModelNumber { get; }
            [DispId(1610747822)]
            string Description { get; }
            [DispId(1610747823)]
            string ModelURL { get; }
            [DispId(1610747824)]
            string UPC { get; }
            [DispId(1610747825)]
            string SerialNumber { get; }
            [DispId(1610747827)]
            string IconURL(string bstrEncodingFormat, int lSizeX, int lSizeY, int lBitDepth);
            [DispId(1610747828)]
            IUPnPServices Services { get; }
        }


        [ComImport]
        [Guid("FDBC0C73-BDA3-4C66-AC4F-F2D96FDAD68C")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        [SuppressUnmanagedCodeSecurity]
        interface IUPnPDevices : IEnumerable
        {
            [DispId(1610747309)]
            int Count { get; }

            [DispId(-4)]
            [TypeLibFunc(TypeLibFuncFlags.FHidden | TypeLibFuncFlags.FRestricted)]
            new IEnumerator GetEnumerator();

            [DispId(0)]
            IUPnPDevice this[string bstrUDN] { get; }
        }

        [ComImport]
        [Guid("3F8C8E9E-9A7A-4DC8-BC41-FF31FA374956")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        [SuppressUnmanagedCodeSecurity]
        interface IUPnPServices : IEnumerable
        {
            [DispId(1610745809)]
            int Count { get; }

            [DispId(-4)]
            [TypeLibFunc(TypeLibFuncFlags.FHidden | TypeLibFuncFlags.FRestricted)]
            new IEnumerator GetEnumerator();

            [DispId(0)]
            IUPnPService this[string bstrServiceId] { get; }
        }

        [ComImport]
        [Guid("A295019C-DC65-47DD-90DC-7FE918A1AB44")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        [SuppressUnmanagedCodeSecurity]
        interface IUPnPService
        {
            [PreserveSig]
            [DispId(1610746309)]
            HRESULT_UPnP QueryStateVariable(string bstrVariableName, out /*VARIANT*/ object pvarValue);
            [PreserveSig]
            [DispId(1610746310)]
            HRESULT_UPnP InvokeAction(string bstrActionName, /*VARIANT*/ object vInActionArgs, ref /*VARIANT*/ object pvOutActionArgs, out /*VARIANT*/ object pvarRetVal);

            [DispId(1610746311)]
            string ServiceTypeIdentifier { get; }
            [DispId(1610746312)]
            void AddCallback(/*IUnknown*/ object pUnkCallback);
            [DispId(1610746313)]
            string Id { get; }
            [DispId(1610746314)]
            int LastTransportStatus { get; }
        }

        enum HRESULT_UPnP : int
        {
            S_OK = 0,
            UPNP_FACILITY_ITF = -2147221504,
            UPNP_E_ROOT_ELEMENT_EXPECTED = UPNP_FACILITY_ITF + 0x0200,
            UPNP_E_DEVICE_ELEMENT_EXPECTED,
            UPNP_E_SERVICE_ELEMENT_EXPECTED,
            UPNP_E_SERVICE_NODE_INCOMPLETE,
            UPNP_E_DEVICE_NODE_INCOMPLETE,
            UPNP_E_ICON_ELEMENT_EXPECTED,
            UPNP_E_ICON_NODE_INCOMPLETE,
            UPNP_E_INVALID_ACTION,
            UPNP_E_INVALID_ARGUMENTS,
            UPNP_E_OUT_OF_SYNC,
            UPNP_E_ACTION_REQUEST_FAILED,
            UPNP_E_TRANSPORT_ERROR,
            UPNP_E_VARIABLE_VALUE_UNKNOWN,
            UPNP_E_INVALID_VARIABLE,
            UPNP_E_DEVICE_ERROR,
            UPNP_E_PROTOCOL_ERROR,
            UPNP_E_ERROR_PROCESSING_RESPONS,
            UPNP_E_DEVICE_TIMEOUT,
            UPNP_E_INVALID_DOCUMENT = UPNP_FACILITY_ITF + 0x0500,
            UPNP_E_EVENT_SUBSCRIPTION_FAILED,
            UPNP_E_ACTION_SPECIFIC_BASE = UPNP_FACILITY_ITF + 0x0300,
            UPNP_E_ACTION_SPECIFIC_MAX = UPNP_E_ACTION_SPECIFIC_BASE + 0x0258,
        }
        #endregion
    }

    public class UPnPPortMapping
    {
        public string RemoteHost { get; set; }
        public ushort ExternalPort { get; set; }
        public string Protocol { get; set; }
        public ushort InternalPort { get; set; }
        public IPAddress InternalClient { get; set; }
        public bool Enabled { get; set; }
        public string PortMappingDescription { get; set; }
        public uint LeaseDuration { get; set; }
    }
}
