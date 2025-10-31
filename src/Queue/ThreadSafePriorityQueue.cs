using System;
using System.Collections.Generic;
using System.Threading;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
  /// <summary>
  /// Thread-safe priority queue implementation using a binary heap
  /// Higher priority values are dequeued first
  /// </summary>
  /// <typeparam name="T">Type of items stored in the queue</typeparam>
  public class ThreadSafePriorityQueue<T>
  {
    private readonly List<PriorityItem<T>> heap;
    private readonly object lockObject = new object();
    private volatile int count = 0;

    /// <summary>
    /// Gets the number of items in the queue
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Gets whether the queue is empty
    /// </summary>
    public bool IsEmpty => count == 0;

    /// <summary>
    /// Initializes a new instance of the ThreadSafePriorityQueue
    /// </summary>
    public ThreadSafePriorityQueue()
    {
      heap = new List<PriorityItem<T>>();
    }

    /// <summary>
    /// Adds an item to the queue with the specified priority
    /// </summary>
    /// <param name="item">Item to add</param>
    /// <param name="priority">Priority value (higher values = higher priority)</param>
    public void Enqueue(T item, int priority)
    {
      lock (lockObject)
      {
        var priorityItem = new PriorityItem<T>(item, priority);
        heap.Add(priorityItem);
        HeapifyUp(heap.Count - 1);
        Interlocked.Increment(ref count);
      }
    }

    /// <summary>
    /// Tries to remove and return the highest priority item from the queue
    /// </summary>
    /// <param name="item">The dequeued item, or default if queue is empty</param>
    /// <returns>True if an item was dequeued, false if queue was empty</returns>
    public bool TryDequeue(out T item)
    {
      item = default;

      lock (lockObject)
      {
        if (heap.Count == 0)
          return false;

        item = heap[0].Item;

        // Move last item to root and remove last item
        heap[0] = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);

        Interlocked.Decrement(ref count);

        // Restore heap property if we still have items
        if (heap.Count > 0)
        {
          HeapifyDown(0);
        }

        return true;
      }
    }

    /// <summary>
    /// Tries to return the highest priority item without removing it
    /// </summary>
    /// <param name="item">The peeked item, or default if queue is empty</param>
    /// <returns>True if an item was peeked, false if queue was empty</returns>
    public bool TryPeek(out T item)
    {
      item = default;

      lock (lockObject)
      {
        if (heap.Count == 0)
          return false;

        item = heap[0].Item;
        return true;
      }
    }

    /// <summary>
    /// Removes all items from the queue
    /// </summary>
    public void Clear()
    {
      lock (lockObject)
      {
        heap.Clear();
        count = 0;
      }
    }

    /// <summary>
    /// Moves an item up the heap to maintain heap property (max-heap)
    /// </summary>
    /// <param name="index">Index of item to move up</param>
    private void HeapifyUp(int index)
    {
      while (index > 0)
      {
        int parentIndex = (index - 1) / 2;

        // If parent has higher or equal priority, we're done
        if (heap[parentIndex].Priority >= heap[index].Priority)
          break;

        // Swap with parent
        (heap[parentIndex], heap[index]) = (heap[index], heap[parentIndex]);
        index = parentIndex;
      }
    }

    /// <summary>
    /// Moves an item down the heap to maintain heap property (max-heap)
    /// </summary>
    /// <param name="index">Index of item to move down</param>
    private void HeapifyDown(int index)
    {
      while (true)
      {
        int leftChild = 2 * index + 1;
        int rightChild = 2 * index + 2;
        int largest = index;

        // Find the largest among parent and children
        if (leftChild < heap.Count && heap[leftChild].Priority > heap[largest].Priority)
          largest = leftChild;

        if (rightChild < heap.Count && heap[rightChild].Priority > heap[largest].Priority)
          largest = rightChild;

        // If parent is largest, heap property is satisfied
        if (largest == index)
          break;

        // Swap with largest child
        (heap[largest], heap[index]) = (heap[index], heap[largest]);
        index = largest;
      }
    }

    /// <summary>
    /// Internal structure to hold item and priority
    /// </summary>
    private readonly struct PriorityItem<TItem>
    {
      public readonly TItem Item;
      public readonly int Priority;

      public PriorityItem(TItem item, int priority)
      {
        Item = item;
        Priority = priority;
      }
    }
  }
}