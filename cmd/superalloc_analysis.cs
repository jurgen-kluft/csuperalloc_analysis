namespace superalloc_analysis
{
    // Targetting 10% allocation waste, this is hard for a specific range of allocation sizes
    // that are close to the page-size of 64 KB. For this we would like to re-route to a region
    // that can deal with 4 KB or 16 KB pages.
    // For example, sizes between 64 KB and 128 KB like 80 KB is automatically wasting 48 KB at
    // the tail. We can reduce this only by going for a page-size of 4 KB / 16 KB.
    //


    public abstract partial class Program
    {
        public enum EWasteTarget : int
		{
            Percent10 = 8,
            Percent25 = 4,
		};

        private static int AllocSizeToBin(ulong size, EWasteTarget target)
        {
            switch (target)
            {
                case EWasteTarget.Percent10:
                {
                    var w = CountLeadingZeros(size);
                    var f = 0x8000000000000000 >> w;
                    var r = 0xFFFFFFFFFFFFFFFF >> (60 - w);
                    var t = ((f - 1) >> 3);
                    size = (size + t) & ~t;
                    var i = (int)((size & r) >> (60 - w)) + ((60 - w) * 8);
                    return i;
                }
                case EWasteTarget.Percent25:
                {
                    var w = CountLeadingZeros(size);
                    var f = 0x8000000000000000 >> w;
                    var r = 0xFFFFFFFFFFFFFFFF >> (61 - w);
                    var t = ((f - 1) >> 2);
                    size = (size + t) & ~t;
                    var i = (int)((size & r) >> (61 - w)) + ((61 - w) * 4);
                    return i;
                }
                default:
                    return -1;
            }
        }

        public class BinMapConfig
        {
            public ulong Count { get; set; }
            public ulong L1Len { get; set; }
            public ulong L2Len { get; set; }
        }

        public class SuperBin_t
		{
            public SuperBin_t(ulong size, int index)
			{
                Size = (uint)size;
                BinIndex = index;
                NumPages = 1;
			}
            public uint Size { get; }
            public int BinIndex { get; set; }
            public int AllocIndex { get; set; }
            public uint NumPages { get; set; }
            public uint Waste { get; set; }
            public uint AllocCount { get; set; }
            public uint BmL1Len { get; set; }
            public uint BmL2Len { get; set; }
		}

        private class SuperAlloc_t
        {
            public List<SuperBin_t> AllocSizes { get; set; } = new List<SuperBin_t>();
            public ulong ChunkSize { get; set; }
            public ulong ChunkCount { get; set; }
        };

        private static BinMapConfig CalcBinMap(ulong allocCount, ulong chunkSize)
        {
            var bm = new BinMapConfig
            {
                Count = (uint)(allocCount)
            };
            if (bm.Count <= 32)
            {
                bm.L1Len = 0;
                bm.L2Len = 0;
            }
            else
            {
                var l2Len = (int)CeilPo2((uint)((allocCount + 15) / 16));
                var l1Len = (l2Len + 15) / 16;
                l1Len = Math.Max(l1Len, 2);
                bm.L1Len = (uint)l1Len;
                bm.L2Len = (uint)l2Len;
            }

            bm.L1Len = CeilPo2(bm.L1Len);
            bm.L2Len = CeilPo2(bm.L2Len);
            return bm;
        }

        public static void Main()
        {
            try
            {
                var wasteTarget = EWasteTarget.Percent25;
				var maxAllocSize = (uint)Mb(256);


				// This is the place to override certain sizes and map them to a higher size
				var allocSizeRemap = new Dictionary<uint, uint>();
                switch (wasteTarget)
                {
                    // remap size     from -> to
                    case EWasteTarget.Percent10:
                        allocSizeRemap.Add(9, 12);
                        allocSizeRemap.Add(10, 12);
                        allocSizeRemap.Add(11, 12);
                        allocSizeRemap.Add(13, 16);
                        allocSizeRemap.Add(14, 16);
                        allocSizeRemap.Add(15, 16);
                        allocSizeRemap.Add(18, 20);
                        allocSizeRemap.Add(22, 24);
                        allocSizeRemap.Add(26, 28);
                        allocSizeRemap.Add(30, 32);
                        break;
                    case EWasteTarget.Percent25:
                        allocSizeRemap.Add(10, 12);
                        allocSizeRemap.Add(14, 16);
                        break;
                }

				var allocSizes = new List<SuperBin_t>();

                for (uint b = 8; b <= maxAllocSize; )
				{
                    var d = b / (uint)wasteTarget;
                    var s = b;
                    while (s < (b<<1))
                    {
                        var bin = AllocSizeToBin(s, wasteTarget);
						Console.WriteLine("AllocSize: {0}, Bin: {1}", s, bin);
						var sbin = new SuperBin_t(s, bin);
						while (allocSizes.Count <= bin)
                        {
							allocSizes.Add(sbin);
                        }
                        s += d;
                    }
                    b = s;
                }

                // Remap certain sizes to another (higher) bin
                foreach(var s2s in allocSizeRemap)
				{
                    var fbin = AllocSizeToBin(s2s.Key, wasteTarget);
                    var tbin = AllocSizeToBin(s2s.Value, wasteTarget);
                    allocSizes[fbin].BinIndex = tbin;
				}

                // Go over all the power-of-2 chunk sizes and determine which allocation sizes to add
                // to each SuperAlloc.
                var pageSize = (uint)Kb(4);
                var allocSizesToDo = new HashSet<uint>();
				foreach (var allocSize in allocSizes)
				{
                    allocSizesToDo.Add(allocSize.Size);
				}

				var allocators = new List<SuperAlloc_t>();

                var chunkSizes = new List<ulong>() { Kb(16), Kb(64), Kb(128), Kb(256), Kb(512), Mb(1), Mb(2), Mb(4), Mb(8), Mb(16), Mb(32), Mb(64), Mb(128), Mb(256), Gb(1)};
				foreach (var chunkSize in chunkSizes)
                {
                    var allocator = new SuperAlloc_t();
                    allocator.ChunkSize = chunkSize;
                    foreach (var allocSize in allocSizes)
                    {
                        if (!allocSizesToDo.Contains(allocSize.Size))
                            continue;
                        if (allocSize.Size > chunkSize)
                            continue;
                        //if (allocSize > pageSize)
                        //{
                        //   allocator.AllocSizes.Add(allocSize);
                        //  allocSizesToDo.Remove(allocSize);
                        // continue;
                        //}

                        // Figure out if this size can be part of this Chunk Size
                        // Go down in chunksize until page-size to try and fit the allocation size
                        var addToAllocator = false;
                        uint numPages = 1;
                        var lowestWaste = pageSize;
                        for (var cs = chunkSize; cs >= pageSize; cs -= pageSize)
                        {
                            var chunkWaste = (uint)(cs % allocSize.Size);
                            if ((chunkWaste <= (uint)(cs / 100)) && chunkWaste < lowestWaste)
                            {
                                numPages = (uint)(cs / pageSize);
                                lowestWaste = chunkWaste;
                                addToAllocator = true;
                                break;
                            }
                        }
                        if (addToAllocator)
						{
                            allocSizesToDo.Remove(allocSize.Size);
                            allocSize.AllocCount = (pageSize * numPages) / allocSize.Size;
                            allocSize.NumPages = numPages;
                            allocSize.Waste = lowestWaste;
                            allocSize.AllocIndex = allocators.Count;
							allocator.AllocSizes.Add(allocSize);
						}
					}
                    allocators.Add(allocator);
                }

                {
                    var allocatorIndex = 0;
                    foreach (var am in allocators)
                    {
                        foreach (var allocSize in am.AllocSizes)
                        {
                            var allocCountPerChunk = am.ChunkSize / allocSize.Size;
                            var chunkSize = am.ChunkSize;
                            var bin = AllocSizeToBin(allocSize.Size, wasteTarget);

                            Console.Write("{0}:", allocatorIndex);
                            Console.Write("{0} AllocSize:{1}, AllocCount:{2}, ChunkSize:{3}, UsedPagesPerChunk:{4}", bin, allocSize.Size.ToByteSize(), allocCountPerChunk, chunkSize.ToByteSize(), allocSize.NumPages);

                            if (allocSize.AllocCount > 1)
                            {
                                var bm = CalcBinMap(allocCountPerChunk, am.ChunkSize);
                                allocSize.BmL1Len = (uint)bm.L1Len;
                                allocSize.BmL2Len = (uint)bm.L2Len;
                                Console.Write(", BinMap({0},{1}):{2}", bm.L1Len, bm.L2Len, 4 + 2 * (bm.L1Len + bm.L2Len));
                            }

                            Console.WriteLine();
                        }

                        allocatorIndex += 1;
                    }

                    Console.WriteLine();
                }

                // Generate C++ Code
                Console.WriteLine("static const s32        c_num_bins = {0};", allocSizes.Count);
                Console.WriteLine("static const superbin_t c_asbins[c_num_bins] = {");
                foreach(var bin in allocSizes)
				{
                    var mb = (int)(bin.Size >> 20) & 0x3FF;
                    var kb = (int)(bin.Size >> 10) & 0x3FF;
                    var b = (int)(bin.Size & 0x3FF);
                    var binIndex = bin.BinIndex;
                    var allocatorIndex = bin.AllocIndex;
                    var useBinMap = (bin.AllocCount > 1) ? 1 : 0;
                    var allocationCount = (int)bin.AllocCount;
                    var binMapL1Len = (int)bin.BmL1Len;
                    var binMapL2Len = (int)bin.BmL2Len;
                    Console.WriteLine("superbin_t({0},{1},{2},{3},{4},{5},{6},{7},{8}){9}", mb, kb, b, binIndex, allocatorIndex, useBinMap, allocationCount, binMapL1Len, binMapL2Len, ",");
				}
                Console.WriteLine("};");

                Console.WriteLine("static const s32    c_num_allocators = {0};", allocators.Count);
				Console.WriteLine("static superalloc_t c_allocators[c_num_allocators] = {");
				foreach (var am in allocators)
                {
                    Console.WriteLine("    superalloc_t({0}),", am.ChunkSize);
                }
                Console.WriteLine("};");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e);
            }
            Console.WriteLine("Done...");
        }
    }
}
