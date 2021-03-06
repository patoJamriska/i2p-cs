﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2PCore.TunnelLayer.I2NP.Messages;
using I2PCore.Data;
using I2PCore.TunnelLayer.I2NP.Data;
using I2PCore.Utils;
using I2PCore.TransportLayer;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System.Collections.Concurrent;

namespace I2PCore.TunnelLayer
{
    public class TransitTunnel: InboundTunnel
    {
        protected I2PIdentHash NextHop;
        public override I2PIdentHash Destination { get { return NextHop; } }

        internal I2PTunnelId SendTunnelId;

        BufLen IVKey;
        BufLen LayerKey;

        public override bool Established { get => true; set => base.Established = value; }

        internal BandwidthLimiter Limiter;

        public TransitTunnel( ITunnelOwner owner, TunnelConfig config, BuildRequestRecord brrec )
            : base( owner, config, 1 )
        {
            Limiter = new BandwidthLimiter( Bandwidth.SendBandwidth, TunnelSettings.TransitTunnelBitrateLimit );

            ReceiveTunnelId = new I2PTunnelId( brrec.ReceiveTunnel );
            NextHop = new I2PIdentHash( new BufRefLen( brrec.NextIdent.Hash.Clone() ) );
            SendTunnelId = new I2PTunnelId( brrec.NextTunnel );

            IVKey = brrec.IVKey.Clone();
            LayerKey = brrec.LayerKey.Clone();
        }

        public override IEnumerable<I2PRouterIdentity> TunnelMembers
        {
            get
            {
                return Enumerable.Empty<I2PRouterIdentity>();
            }
        }

        public override bool Exectue()
        {
            return HandleReceiveQueue();
        }

#if LOG_ALL_TUNNEL_TRANSFER
        ItemFilterWindow<HashedItemGroup> FilterMessageTypes = new ItemFilterWindow<HashedItemGroup>( TickSpan.Seconds( 30 ), 2 );
#endif

        private bool HandleReceiveQueue()
        {
            var tdmsgs = new List<TunnelDataMessage>();

            if ( ReceiveQueue.IsEmpty ) return true;

            while ( ReceiveQueue.TryDequeue( out var message ) )
            {
                if ( message.MessageType == I2NPMessage.MessageTypes.TunnelData )
                {
                    // Just drop the non-TunnelData
                    tdmsgs.Add( (TunnelDataMessage)message );
                }
            }

            if ( tdmsgs.Any() )
            {
                return HandleTunnelData( tdmsgs );
            }

            return true;
        }

#if LOG_ALL_TUNNEL_TRANSFER
        PeriodicLogger LogDataSent = new PeriodicLogger( 15 );
#endif

        private bool HandleTunnelData( IEnumerable<TunnelDataMessage> msgs )
        {
            EncryptTunnelMessages( msgs );

#if LOG_ALL_TUNNEL_TRANSFER
            LogDataSent.Log( () => "TransitTunnel " + Destination.Id32Short + " TunnelData sent." );
#endif
            var dropped = 0;
            foreach ( var one in msgs )
            {
                if ( Limiter.DropMessage() )
                {
                    ++dropped;
                    continue;
                }

                one.TunnelId = SendTunnelId;
                Bandwidth.DataSent( one.Payload.Length );
                TransportProvider.Send( Destination, one );
            }

#if LOG_ALL_TUNNEL_TRANSFER
            if ( dropped > 0 )
            {
                Logging.LogDebug( () => string.Format( "{0} bandwidth limit. {1} dropped messages. {2}", this, dropped, Bandwidth ) );
            }
#endif

            return true;
        }

        private void EncryptTunnelMessages( IEnumerable<TunnelDataMessage> msgs )
        {
            var cipher = new CbcBlockCipher( new AesEngine() );

            foreach ( var msg in msgs )
            {
                msg.IV.AesEcbEncrypt( IVKey.ToByteArray() );

                cipher.Init( true, LayerKey.ToParametersWithIV( msg.IV ) );
                cipher.ProcessBytes( msg.EncryptedWindow );

                msg.IV.AesEcbEncrypt( IVKey.ToByteArray() );
            }
        }
    }
}
