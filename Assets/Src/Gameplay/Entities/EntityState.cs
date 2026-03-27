using System;

namespace PendingName.Entities {
	
	[Flags]
	public enum EntityState : byte {
		None = 0,
		Alive = 1 << 0, 	// 0001
		Dead = 1 << 1,		// 0010
		Burning = 1 << 2, 	// 0100
		Freezing = 1 << 3,	// 1000
	}
}