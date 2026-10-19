using System.Collections.Generic;

namespace BlackoutProtocol.AI.Hunter.Logic
{
    /// <summary>Min-heap ordered by f, then h, node id and insertion order.</summary>
    public sealed class HunterPriorityQueue
    {
        public struct Entry
        {
            public readonly int Node;
            public readonly double G;
            public readonly double F;
            public readonly double H;
            internal readonly long Order;

            internal Entry(int node, double g, double f, double h, long order)
            {
                Node = node;
                G = g;
                F = f;
                H = h;
                Order = order;
            }
        }

        private readonly List<Entry> heap = new List<Entry>();
        private long nextOrder;
        public int Count => heap.Count;

        public bool Enqueue(int node, double g, double h)
        {
            double f = g + h;
            if (node < 0 || !Finite(g) || !Finite(h) || !Finite(f) || g < 0 || h < 0) return false;
            var entry = new Entry(node, g, f, h, nextOrder++);
            int index = heap.Count;
            heap.Add(entry);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(heap[parent], entry) <= 0) break;
                heap[index] = heap[parent];
                index = parent;
            }
            heap[index] = entry;
            return true;
        }

        public bool TryDequeue(out Entry entry)
        {
            if (heap.Count == 0) { entry = default(Entry); return false; }
            entry = heap[0];
            int lastIndex = heap.Count - 1;
            Entry last = heap[lastIndex];
            heap.RemoveAt(lastIndex);
            if (lastIndex == 0) return true;
            int index = 0;
            while (index * 2 + 1 < heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < heap.Count && Compare(heap[child + 1], heap[child]) < 0) child++;
                if (Compare(last, heap[child]) <= 0) break;
                heap[index] = heap[child];
                index = child;
            }
            heap[index] = last;
            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static int Compare(Entry a, Entry b)
        {
            int comparison = a.F.CompareTo(b.F);
            if (comparison != 0) return comparison;
            comparison = a.H.CompareTo(b.H);
            if (comparison != 0) return comparison;
            comparison = a.Node.CompareTo(b.Node);
            return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
        }
    }
}
