/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsPlaybackAnalyzer.Capture.cs
 * Purpose: Owns loopback capture, sample decoding, FFT bands and stereo peak/RMS measurements.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsPlaybackAnalyzer
{
    /// <summary>Uses the selected render endpoint for output loopback with the existing capture cadence.</summary>
    /// <param name="device">Selected Windows output endpoint retained by the owning capture session.</param>
    private sealed class OutputCapture(MMDevice device) : WasapiCapture(device, false, 50)
    {

        /// <summary>Enables Windows output loopback for the capture client.</summary>
        protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
            base.GetAudioClientStreamFlags() | AudioClientStreamFlags.Loopback;
    }

    private sealed class CaptureSession : IDisposable
    {
        private static readonly Guid PcmSubtype = new("00000001-0000-0010-8000-00aa00389b71");
        private static readonly Guid FloatSubtype = new("00000003-0000-0010-8000-00aa00389b71");
        private readonly object dataGate = new();
        private readonly MMDevice device;
        private readonly OutputCapture capture;
        private readonly int sampleRate;
        private readonly int channels;
        private readonly int sampleBytes;
        private readonly int blockAlign;
        private readonly bool floatingPoint;
        private readonly float[][] ring;
        private readonly float[] window = new float[TransformSize];
        private readonly Complex[] transform = new Complex[TransformSize];
        private readonly double[] powers = new double[TransformSize / 2 + 1];
        private readonly double[] frequencies;
        private readonly int[] firstBin = new int[BandCount];
        private readonly int[] lastBin = new int[BandCount];
        private readonly double fftGain;
        private readonly long startedAt = Environment.TickCount64;
        private PlaybackAnalysis latest;
        private int writeIndex;
        private int filledFrames;
        private int meterFrames;
        private double leftSquares;
        private double rightSquares;
        private double leftPeak;
        private double rightPeak;
        private double leftHold = FloorDbfs;
        private double rightHold = FloorDbfs;
        private long leftHeldAt;
        private long rightHeldAt;
        private long lastPublished;
        private long lastData;
        private bool disposed;
        private volatile bool stopped;

        public string DeviceId { get; }
        public bool Stopped => stopped;

        /// <summary>Starts bounded loopback capture for the selected Windows output endpoint.</summary>
        /// <param name="selected">Windows render endpoint whose lifetime transfers to this capture session.</param>
        public CaptureSession(MMDevice selected)
        {
            device = selected;
            OutputCapture? created = null;
            try
            {
                DeviceId = selected.ID;
                created = new OutputCapture(selected);
                var format = created.WaveFormat;
                sampleRate = format.SampleRate;
                channels = format.Channels;
                sampleBytes = format.BitsPerSample / 8;
                blockAlign = format.BlockAlign;
                var encoding = format.Encoding;
                if (encoding == WaveFormatEncoding.Extensible)
                {
                    if (format is not WaveFormatExtensible extended) { throw new NotSupportedException("Unknown extensible audio format."); }
                    encoding = extended.SubFormat == FloatSubtype ? WaveFormatEncoding.IeeeFloat
                        : extended.SubFormat == PcmSubtype ? WaveFormatEncoding.Pcm
                        : throw new NotSupportedException("Unsupported extensible audio subtype.");
                }
                floatingPoint = encoding == WaveFormatEncoding.IeeeFloat;
                if (sampleRate is < 8000 or > 192000 || channels is < 1 or > 8 || blockAlign != channels * sampleBytes ||
                    (floatingPoint ? format.BitsPerSample != 32 : encoding != WaveFormatEncoding.Pcm || format.BitsPerSample is not (16 or 24 or 32)))
                { throw new NotSupportedException("Playback analysis supports float32 or PCM16/24/32, 1–8 channels, 8–192 kHz."); }

                ring = new float[channels][];
                for (var channel = 0; channel < channels; channel++) { ring[channel] = new float[TransformSize]; }
                double windowSum = 0;
                for (var index = 0; index < TransformSize; index++)
                {
                    window[index] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * index / (TransformSize - 1))));
                    windowSum += window[index];
                }
                // NAudio's forward FFT already divides by N. Correct the Hann coherent gain.
                fftGain = TransformSize / windowSum;
                frequencies = BandCenters(sampleRate);
                for (var band = 0; band < BandCount; band++)
                {
                    var low = band == 0 ? 20 : Math.Sqrt(frequencies[band - 1] * frequencies[band]);
                    var high = band == BandCount - 1 ? Math.Min(20000d, sampleRate / 2d) : Math.Sqrt(frequencies[band] * frequencies[band + 1]);
                    firstBin[band] = Math.Clamp((int)Math.Ceiling(low * TransformSize / sampleRate), 1, TransformSize / 2);
                    lastBin[band] = Math.Clamp((int)Math.Floor(high * TransformSize / sampleRate), 1, TransformSize / 2);
                    if (lastBin[band] < firstBin[band])
                    {
                        // Low bands narrower than an FFT bin share the nearest measured bin.
                        firstBin[band] = lastBin[band] = Math.Clamp((int)Math.Round(frequencies[band] * TransformSize / sampleRate), 1, TransformSize / 2);
                    }
                }
                latest = new(false, "system-output", sampleRate, TransformSize, channels, FloorBands(), frequencies,
                    null, null, null, null, null, null, null, "Waiting for system output.");
                capture = created;
                capture.DataAvailable += OnData;
                capture.RecordingStopped += OnStopped;
                capture.StartRecording();
            }
            catch
            {
                try { created?.Dispose(); }
                finally { selected.Dispose(); }
                throw;
            }
        }

        /// <summary>Returns the latest analysis with independent band arrays.</summary>
        public PlaybackAnalysis Snapshot()
        {
            lock (dataGate)
            {
                if (!stopped && Environment.TickCount64 - (lastData > 0 ? lastData : startedAt) > 150)
                {
                    return latest with
                    {
                        Available = true,
                        BandDbfs = FloorBands(),
                        FrequenciesHz = (double[])frequencies.Clone(),
                        LeftPeakDbfs = FloorDbfs,
                        RightPeakDbfs = channels > 1 ? FloorDbfs : null,
                        LeftRmsDbfs = FloorDbfs,
                        RightRmsDbfs = channels > 1 ? FloorDbfs : null,
                        LeftPeakHoldDbfs = FloorDbfs,
                        RightPeakHoldDbfs = channels > 1 ? FloorDbfs : null,
                        Status = "No recent audio data."
                    };
                }
                return Copy(latest);
            }
        }

        /// <summary>Decodes captured output samples and updates bounded analysis buffers.</summary>
        /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
        /// <param name="args">Event data supplied by the UI or capture framework.</param>
        private void OnData(object? sender, WaveInEventArgs args)
        {
            lock (dataGate)
            {
                if (disposed || stopped || args.BytesRecorded <= 0) { return; }
                try
                {
                    var now = Environment.TickCount64;
                    if (lastData > 0 && now - lastData > 150) { ResetSamples(); }
                    lastData = now;
                    var bytes = Math.Min(args.BytesRecorded, args.Buffer.Length);
                    var frames = bytes / blockAlign;
                    // Bound callback work even if a driver supplies an unexpectedly large packet.
                    var firstFrame = Math.Max(0, frames - sampleRate / 5);
                    if (firstFrame > 0) { ResetSamples(); }
                    for (var frame = firstFrame; frame < frames; frame++)
                    {
                        var offset = frame * blockAlign;
                        for (var channel = 0; channel < channels; channel++)
                        {
                            var sample = Decode(args.Buffer.AsSpan(offset + channel * sampleBytes, sampleBytes));
                            ring[channel][writeIndex] = sample;
                            if (channel == 0) { leftSquares += sample * (double)sample; leftPeak = Math.Max(leftPeak, Math.Abs(sample)); }
                            else if (channel == 1) { rightSquares += sample * (double)sample; rightPeak = Math.Max(rightPeak, Math.Abs(sample)); }
                        }
                        writeIndex = (writeIndex + 1) % TransformSize;
                        filledFrames = Math.Min(TransformSize, filledFrames + 1);
                        meterFrames++;
                    }
                    // At most one transform per callback and at most 20 transforms a second.
                    if (filledFrames == TransformSize && meterFrames >= sampleRate / 20 && now - lastPublished >= 50)
                    { Publish(now); }
                }
                catch (Exception error)
                {
                    stopped = true;
                    latest = Unavailable($"Playback analysis unavailable: {error.Message}");
                }
            }
        }

        /// <summary>Converts one encoded audio sample to normalized floating-point amplitude.</summary>
        /// <param name="value">One encoded audio sample in the capture endpoint format.</param>
        private float Decode(ReadOnlySpan<byte> value)
        {
            if (floatingPoint)
            {
                var sample = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(value));
                return float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0;
            }
            return sampleBytes switch
            {
                2 => BinaryPrimitives.ReadInt16LittleEndian(value) / 32768f,
                3 => ((value[0] | value[1] << 8 | value[2] << 16) << 8 >> 8) / 8388608f,
                // Valid 24-in-32 extensible PCM is left aligned in its signed 32-bit container.
                4 => (float)(BinaryPrimitives.ReadInt32LittleEndian(value) / 2147483648d),
                _ => 0
            };
        }

        /// <summary>Calculates FFT bands and publishes the latest bounded audio analysis.</summary>
        /// <param name="now">Monotonic timestamp used for audio analysis timing.</param>
        private void Publish(long now)
        {
            Array.Clear(powers);
            for (var channel = 0; channel < channels; channel++)
            {
                for (var index = 0; index < TransformSize; index++)
                {
                    transform[index].X = ring[channel][(writeIndex + index) % TransformSize] * window[index];
                    transform[index].Y = 0;
                }
                FastFourierTransform.FFT(true, 12, transform);
                for (var bin = 1; bin < powers.Length; bin++)
                {
                    var gain = bin == TransformSize / 2 ? fftGain : 2 * fftGain;
                    var real = (double)transform[bin].X;
                    var imaginary = (double)transform[bin].Y;
                    powers[bin] += (real * real + imaginary * imaginary) * gain * gain / channels;
                }
            }
            var bands = new double[BandCount];
            for (var band = 0; band < BandCount; band++)
            {
                double peakPower = 0;
                for (var bin = firstBin[band]; bin <= lastBin[band]; bin++) { peakPower = Math.Max(peakPower, powers[bin]); }
                // The bar is the strongest measured FFT bin in the band, not invented interpolation.
                bands[band] = PowerDbfs(peakPower);
            }
            var leftDbfs = AmplitudeDbfs(leftPeak);
            var rightDbfs = AmplitudeDbfs(rightPeak);
            UpdateHold(leftDbfs, now, ref leftHold, ref leftHeldAt);
            UpdateHold(rightDbfs, now, ref rightHold, ref rightHeldAt);
            latest = new(true, "system-output", sampleRate, TransformSize, channels, bands, frequencies,
                leftDbfs, channels > 1 ? rightDbfs : null,
                PowerDbfs(leftSquares / meterFrames), channels > 1 ? PowerDbfs(rightSquares / meterFrames) : null,
                leftHold, channels > 1 ? rightHold : null, DateTimeOffset.UtcNow,
                channels > 2 ? $"System output · {channels} channels · front L/R meters" : "System output");
            lastPublished = now;
            ResetMeters();
        }

        /// <summary>Updates a channel's held peak using the monotonic clock.</summary>
        /// <param name="peakDbfs">Current channel peak in dBFS, bounded by the analysis floor and zero.</param>
        /// <param name="now">Monotonic timestamp used for audio analysis timing.</param>
        /// <param name="heldDbfs">Held peak in dBFS, updated by reference.</param>
        /// <param name="heldAt">Monotonic timestamp of the held peak, updated by reference.</param>
        private void UpdateHold(double peakDbfs, long now, ref double heldDbfs, ref long heldAt)
        {
            if (heldAt == 0 || peakDbfs >= heldDbfs) { heldDbfs = peakDbfs; heldAt = now; }
            else
            {
                var releaseSeconds = Math.Max(0, now - Math.Max(lastPublished, heldAt + 1000)) / 1000d;
                heldDbfs = Math.Max(peakDbfs, Math.Max(FloorDbfs, heldDbfs - 12 * releaseSeconds));
            }
        }

        /// <summary>Converts linear amplitude to the bounded dBFS display scale.</summary>
        /// <param name="value">Nonnegative linear sample amplitude; one corresponds to 0 dBFS.</param>
        private static double AmplitudeDbfs(double value) => value > 0 ? Math.Clamp(20 * Math.Log10(value), FloorDbfs, 0) : FloorDbfs;

        /// <summary>Converts linear power to the bounded dBFS display scale.</summary>
        /// <param name="value">Nonnegative mean-square sample power; one corresponds to 0 dBFS.</param>
        private static double PowerDbfs(double value) => value > 0 ? Math.Clamp(10 * Math.Log10(value), FloorDbfs, 0) : FloorDbfs;

        /// <summary>Clears the FFT sample window and its write position.</summary>
        private void ResetSamples()
        {
            // A discontinuity must not reuse old samples as new signal.
            filledFrames = 0;
            writeIndex = 0;
            leftHold = rightHold = FloorDbfs;
            leftHeldAt = rightHeldAt = 0;
            latest = latest with
            {
                Available = false,
                BandDbfs = FloorBands(),
                LeftPeakDbfs = null,
                RightPeakDbfs = null,
                LeftRmsDbfs = null,
                RightRmsDbfs = null,
                LeftPeakHoldDbfs = null,
                RightPeakHoldDbfs = null,
                Status = "Collecting system output."
            };
            ResetMeters();
        }

        /// <summary>Clears the current channel meters and peak holds.</summary>
        private void ResetMeters()
        {
            meterFrames = 0;
            leftSquares = rightSquares = leftPeak = rightPeak = 0;
        }

        /// <summary>Records capture failure and stops reporting stale audio levels.</summary>
        /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
        /// <param name="args">Event data supplied by the UI or capture framework.</param>
        private void OnStopped(object? sender, StoppedEventArgs args)
        {
            lock (dataGate)
            {
                if (disposed) { return; }
                stopped = true;
                latest = Unavailable(args.Exception is { } error ? $"Playback analysis stopped: {error.Message}" : "Playback analysis stopped.");
            }
            // Never dispose here: NAudio can raise this event on the very thread Dispose joins.
        }

        /// <summary>Releases resources owned by this instance.</summary>
        public void Dispose()
        {
            lock (dataGate)
            {
                if (disposed) { return; }
                disposed = true;
            }
            // Release dataGate before joining the capture thread, whose callbacks also take it.
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;
            try { capture.Dispose(); }
            finally { device.Dispose(); }
        }
    }
}
