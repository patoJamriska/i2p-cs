﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using I2PCore.Utils;

namespace I2PCore.Data
{
    public class I2PLease : I2PType
    {
        public static readonly TickSpan LeaseLifetime = TickSpan.Minutes( 10 );

        public I2PIdentHash TunnelGw;
        public I2PTunnelId TunnelId;
        public I2PDate EndDate;

        public I2PLease( I2PIdentHash tunnelgw, I2PTunnelId tunnelid, I2PDate enddate )
        {
            TunnelGw = tunnelgw;
            TunnelId = tunnelid;
            EndDate = enddate;
        }

        public I2PLease( I2PIdentHash tunnelgw, I2PTunnelId tunnelid )
        {
            TunnelGw = tunnelgw;
            TunnelId = tunnelid;
            EndDate = new I2PDate( 
                DateTime.UtcNow 
                    + TimeSpan.FromSeconds( LeaseLifetime.ToSeconds ) );
        }

        public I2PLease( BufRef reader )
        {
            TunnelGw = new I2PIdentHash( reader );
            TunnelId = new I2PTunnelId( reader );

            EndDate = new I2PDate( reader );
        }

        public void Write( BufRefStream dest )
        {
            TunnelGw.Write( dest );
            TunnelId.Write( dest );
            EndDate.Write( dest );
        }

        public override string ToString()
        {
            return $"I2PLease GW {TunnelGw.Id32Short}, Id {TunnelId}, Exp {EndDate}";
        }
    }
}
