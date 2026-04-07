dotnet run --project SortTitan.Sorter -- --input "d:\Work\SortTitan\gen_1mb.txt" --output "d:\Work\SortTitan\sorted_1mb.txt" --temp "d:\Work\SortTitan\sort_temp" --mem-bytes 1073741824 --max-inflight 2 --spill-parallelism 1 --merge-fanin 128
----mem-bytes 1073741824 
--max-inflight 2 
--spill-parallelism 1 
--merge-fanin 128
Input:  d:\Work\SortTitan\gen_5gb.txt
Output: d:\Work\SortTitan\sorted_5gb.txt
Temp:   d:\Work\SortTitan\sort_temp
Sorting...
Done. Wrote 5368709139 bytes.
Total time: 00:09:14.8761257
Split time: 00:06:06.2333911
Merge time: 00:03:08.6359089
Merge passes: 0
Temp files: 34
Input bytes: 5368709139
Temp bytes:  5529808161
Approx I/O bytes: 16267226439
---
--mem-bytes 8589934592 
--max-inflight 2 
--spill-parallelism 1 
--merge-fanin 12
and updated initialCapacity
Done. Wrote 5368709139 bytes.
Total time: 00:07:06.3334874
Split time: 00:03:56.2077877
Merge time: 00:03:10.1228361
Merge passes: 1
Temp files: 37
Input bytes: 5368709139
Temp bytes:  10898517300
Approx I/O bytes: 21635935578
--
--spill-parallelism 2