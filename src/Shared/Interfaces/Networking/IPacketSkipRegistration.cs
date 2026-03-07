using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONI_MP.Networking.Packets.Architecture
{
	/// <summary>
	/// Prevents the automated registry from registering the inheriting class as a packet if it inherits IPacket
	/// </summary>
	public interface IPacketSkipsRegistration
	{
	}
}
