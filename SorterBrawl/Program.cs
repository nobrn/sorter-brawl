﻿using System;
using System.IO;
using System.Drawing;

using SorterBrawl.Sorters;
using SorterBrawl.Audio;
using SorterBrawl.Frames;
using SorterBrawl.Frames.Stylers;

namespace SorterBrawl
{
    class Program
    {
        static void Main(string[] args)
        {
            int[] array = GetRandomArray(10, seed: 32);

            Sorter[] sorters = new Sorter[] {
                new MergeSort(SorterTheme.Warm),
                new QuickSortReverse(SorterTheme.Cold)
            };

            var profile = new Profile(
                new FrameProfile
                {
                    Width = 1920,
                    Height = 1080,
                },
                new AudioProfile { },
                frameLimit: 50,
                frameCountDownscale: 1
            );

            Animator animator = new Animator(array, sorters, profile);

            string animationsDir = @"..\..\..\Animations";
            Directory.CreateDirectory(animationsDir);

            animator.SaveAnimation(animationsDir);
        }

        static int[] GetRandomArray(int size, int? seed = null)
        {
            int[] array = new int[size];

            Random rand = seed.HasValue ? new Random(seed.Value) : new Random();

            for (int i = 1; i <= size; i++)
            {
                int j;
                do
                {
                    j = rand.Next(0, size);
                } while (array[j] != 0);
                array[j] = i;
            }

            return array;
        }

        static int[] GetReverseArray(int size)
        {
            int[] result = new int[size];
            for (int i = 0; i < size; i++)
                result[i] = size - i;
            return result;
        }

        static void RandomizeArray(int[] array, int? seed = null)
        {
            Random rand = seed.HasValue ? new Random(seed.Value) : new Random();

            for (int i = 0; i < array.Length; i++)
            {
                int j = rand.Next(0, array.Length);
                int temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }
    }
}
