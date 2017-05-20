﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2PCore.Utils;
using I2PCore.Router;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace I2PCore.Transport.NTCP
{
    public class NTCPHost
    {
        Thread Worker;
        bool Terminated = false;

        public event Action<ITransport> ConnectionCreated;

        List<NTCPClientIncoming> Clients = new List<NTCPClientIncoming>();

        public NTCPHost()
        {
            RouterContext.Inst.NetworkSettingsChanged += new Action( NetworkSettingsChanged );

            Worker = new Thread( () => Run() );
            Worker.Name = "NTCPHost";
            Worker.IsBackground = true;
            Worker.Start();
        }

        void Run()
        {
            try
            {
                while ( !Terminated )
                {
                    var listener = CreateListener();

                    try
                    {
                        listener.BeginAccept( new AsyncCallback( DoAcceptTcpClientCallback ), listener );

                        while ( !Terminated )
                        {
                            Thread.Sleep( 2000 );

                            lock ( Clients )
                            {
                                var terminated = Clients.Where( c => c.Terminated ).ToArray();
                                foreach ( var one in terminated )
                                {
                                    Clients.Remove( one );
                                }
                            }

                            if ( SettingsChanged )
                            {
                                SettingsChanged = false;

                                listener.Shutdown( SocketShutdown.Both );
                                listener.Close();

                                Thread.Sleep( 3000 );

                                listener = CreateListener();
                                listener.BeginAccept( new AsyncCallback( DoAcceptTcpClientCallback ), listener );

                                DebugUtils.LogInformation( "NTCPHost: Running with new network settings. " +
                                    listener.LocalEndPoint.ToString() + ":" + RouterContext.Inst.TCPPort.ToString() + 
                                    " (" + RouterContext.Inst.ExtAddress.ToString() + ")" );
                            }
                        }
                    }
                    catch ( ThreadAbortException ex )
                    {
                        DebugUtils.Log( ex );
                    }
                    catch ( Exception ex )
                    {
                        DebugUtils.Log( ex );
                    }
                }
            }
            finally
            {
                Terminated = true;
                Worker = null;
            }
        }

        private static Socket CreateListener()
        {
            Socket listener;
            var ipaddr = RouterContext.Inst.Address;
            listener = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            listener.Bind( new IPEndPoint( ipaddr, RouterContext.Inst.TCPPort ) );
            listener.Listen( 20 );
            return listener;
        }

        bool SettingsChanged = false;

        public void NetworkSettingsChanged()
        {
            SettingsChanged = true;
        }

        void DoAcceptTcpClientCallback( IAsyncResult ar )
        {
            if ( !ar.IsCompleted ) return;
            bool docontinue = true;

            var listener = (Socket)ar.AsyncState;

            try
            {
                var socket = listener.EndAccept( ar );

                var ntcpc = new NTCPClientIncoming( socket );
                DebugUtils.LogDebug( "NTCPHost: incoming connection " + ntcpc.DebugId + " from " + socket.RemoteEndPoint.ToString() + " created." );

                if ( ConnectionCreated != null ) ConnectionCreated( ntcpc );

                ntcpc.Connect();
                lock ( Clients )
                {
                    Clients.Add( ntcpc );
                }
            }
            catch ( ObjectDisposedException )
            {
                docontinue = false;
            }
            catch ( Exception ex )
            {
                DebugUtils.Log( ex );
            }

            try
            {
                if ( docontinue ) listener.BeginAccept( new AsyncCallback( DoAcceptTcpClientCallback ), listener );
            }
            catch ( Exception ex )
            {
                DebugUtils.Log( ex );
            }
        }
    }
}
