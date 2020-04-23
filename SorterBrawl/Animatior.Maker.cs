﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using static System.Math;

using SorterBrawl.Sorters;
using SorterBrawl.Audio;
using SorterBrawl.Frames;

namespace SorterBrawl
{
    using FlagList = List<Tuple<int, FlagType>>;

    partial class Animator
    {
        /// <summary>
        /// Base class for making and saving visualizations.
        /// </summary>
        private abstract class Maker
        {
            public int FrameCount { get; protected set; }

            public int ComparisonCount { get; protected set; }

            protected string savePath;

            protected int[] array;

            protected int maxValue;
            protected int minValue;

            protected Maker(string savePath, int[] array)
            {
                this.savePath = savePath;

                this.array = array;

                minValue = array.Min();
                maxValue = array.Max();
            }

            public abstract void UpdateFrame(Sorter sender, FlagList flagList);

            protected abstract void SaveFrame(Sorter sender, FlagList flagList);

            public abstract void Finish();

            public abstract bool HasCompleted();
        }

        private class FrameMaker : Maker
        {
            string imagePath;

            FrameProfile profile;

            Bitmap bitmap;

            Graphics graphics;

            Dictionary<int, FlagData> indexFlags = new Dictionary<int, FlagData>();

            public FrameMaker(string savePath, FrameProfile profile, int[] array)
              : base(savePath, array)
            {
                imagePath = savePath + @"\images";
                Directory.CreateDirectory(imagePath);

                this.profile = profile;

                bitmap = new Bitmap(profile.Width, profile.Height);
                graphics = Graphics.FromImage(bitmap);
            }

            public override void UpdateFrame(Sorter sender, FlagList flagList)
            {
                profile.Styler.Clear(graphics);

                lock (array)
                    lock (bitmap)
                        lock (graphics)
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                FlagData flagData;
                                lock (indexFlags)
                                    indexFlags.TryGetValue(i, out flagData);

                                DrawData data = new DrawData(graphics, flagData.sorter, profile.Width, profile.Height, i,
                                  array.Length, array[i], minValue, maxValue, flagData.flagType);

                                profile.Styler.DrawElement(data);
                            }

                            if (ComparisonCount++ % profile.FrameCountDownscale == 0)
                            {
                                UpdateIndexFlags(sender, flagList);
                                SaveFrame(sender, flagList);
                            }
                        }
            }

            protected override void SaveFrame(Sorter sender, FlagList flagList)
            {
                lock (bitmap)
                    bitmap.Save(imagePath + @$"\frame_{++FrameCount}.png", ImageFormat.Png);
            }

            public override void Finish()
            {
                graphics.Dispose();
                bitmap.Dispose();
            }

            public override bool HasCompleted()
            {
                return FrameCount >= profile.FrameLimit;
            }

            void UpdateIndexFlags(Sorter sender, FlagList flagList)
            {
                lock (indexFlags)
                {
                    var itemsToRemove = indexFlags.Where(x => x.Value.sorter == sender).ToArray();
                    foreach (var item in itemsToRemove)
                        indexFlags.Remove(item.Key);

                    foreach (var flag in flagList)
                        indexFlags[flag.Item1] = new FlagData()
                        {
                            flagType = flag.Item2,
                            sorter = sender
                        };
                }
            }
        }

        private class AudioMaker : Maker
        {
            AudioProfile profile;

            string wavFilePath;

            const string wavFileName = "audio.wav";

            FileStream stream;

            BinaryWriter writer;

            int samplesPerFrame;

            const int SamplesPerSecond = 44100;

            public AudioMaker(string savePath, AudioProfile profile, int[] array)
              : base(savePath, array)
            {
                this.profile = profile;

                wavFilePath = savePath + '\\' + wavFileName;

                samplesPerFrame = SamplesPerSecond / profile.FramesPerSecond;

                stream = new FileStream(wavFilePath, FileMode.Create);
                writer = new BinaryWriter(stream);

                writer.Write(GetWavHeaderBytes(0.0).ToArray());
            }

            public override void UpdateFrame(Sorter sender, FlagList flagList)
            {
                if (ComparisonCount++ % profile.FrameCountDownscale == 0)
                    SaveFrame(sender, flagList);
            }

            protected override void SaveFrame(Sorter sender, FlagList flagList)
            {
                for (int i = 0; i < samplesPerFrame; i++)
                {
                    double t = (double)i / SamplesPerSecond;
                    short s = 0;
                    foreach (var flag in flagList)
                    {
                        if (flag.Item1 < 0 || flag.Item1 >= array.Length)
                            continue;

                        double amplFraction = profile.AmplitudePortion(i, samplesPerFrame);

                        // TODO: Parameterize this
                        double freq = ExponentialMap(1.005, array[flag.Item1], minValue, maxValue,
                          profile.MinFrequency, profile.MaxFrequency);

                        s += (short)(profile.Amplitude * amplFraction * Sin(t * freq * 2.0 * PI));
                    }

                    lock (writer)
                        writer.Write(s);
                }

                FrameCount++;
            }

            public override void Finish()
            {
                writer.Dispose();
                stream.Dispose();

                byte[] wavBytes = File.ReadAllBytes(wavFilePath);

                double duration = (double)FrameCount / profile.FramesPerSecond;

                GetWavHeaderBytes(duration).CopyTo(wavBytes);

                File.WriteAllBytes(wavFilePath, wavBytes);
            }

            public override bool HasCompleted()
            {
                throw new NotImplementedException();
            }

            static List<byte> GetWavHeaderBytes(double duration)
            {
                int RIFF = 0x46464952;
                int WAVE = 0x45564157;
                int formatChunkSize = 16;
                int headerSize = 8;
                int format = 0x20746D66;
                short formatType = 1;
                short tracks = 1;
                short bitsPerSample = 16;
                short frameSize = (short)(tracks * ((bitsPerSample + 7) / 8));
                int bytesPerSecond = SamplesPerSecond * frameSize;
                int waveSize = 4;
                int data = 0x61746164;
                int samples = (int)(SamplesPerSecond * duration);
                int dataChunkSize = samples * frameSize;
                int fileSize = waveSize + headerSize + formatChunkSize + headerSize + dataChunkSize;

                List<byte> byteList = new List<byte>();

                byteList.AddRange(BitConverter.GetBytes(RIFF));
                byteList.AddRange(BitConverter.GetBytes(fileSize));
                byteList.AddRange(BitConverter.GetBytes(WAVE));
                byteList.AddRange(BitConverter.GetBytes(format));
                byteList.AddRange(BitConverter.GetBytes(formatChunkSize));
                byteList.AddRange(BitConverter.GetBytes(formatType));
                byteList.AddRange(BitConverter.GetBytes(tracks));
                byteList.AddRange(BitConverter.GetBytes(SamplesPerSecond));
                byteList.AddRange(BitConverter.GetBytes(bytesPerSecond));
                byteList.AddRange(BitConverter.GetBytes(frameSize));
                byteList.AddRange(BitConverter.GetBytes(bitsPerSample));
                byteList.AddRange(BitConverter.GetBytes(data));
                byteList.AddRange(BitConverter.GetBytes(dataChunkSize));

                return byteList;
            }

            static double ExponentialMap(double b, double x, double x1, double x2, double y1, double y2)
            {
                return y1 + (y2 - y1) * (Pow(b, x) - Pow(b, x1)) / (Pow(b, x2) - Pow(b, x1));
            }
        }
    }
}