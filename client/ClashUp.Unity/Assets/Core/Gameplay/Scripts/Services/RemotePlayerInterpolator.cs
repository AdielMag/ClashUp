using System.Collections.Generic;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Entity interpolation for remote players (Gambetta, "Entity Interpolation").
    /// Remote players are NOT simulated on the client — they are rendered purely from
    /// authoritative snapshots. Each snapshot is buffered with its server timestamp; the
    /// renderer plays them back ~<see cref="_interpolationDelayMs"/> in the past, lerping
    /// between the two samples that bracket a render clock. This trades a small, fixed
    /// latency for perfectly smooth remote motion that survives an occasional dropped packet.
    /// See netcode-architecture.md.
    /// </summary>
    public sealed class RemotePlayerInterpolator
    {
        private struct Sample
        {
            public double TimeMs;
            public float X;
            public float Z;
            public float Yaw;
            public float Health;
        }

        private sealed class Track
        {
            public readonly Sample[] Buffer = new Sample[Capacity];
            public int Count;
            public int Head; // index of the oldest sample
        }

        private const int Capacity = 32;

        private readonly Dictionary<string, Track> _tracks = new();
        private readonly List<string> _playerIds = new();

        private double _interpolationDelayMs = 66.0;
        private double _renderClockMs;
        private double _latestServerMs;
        private bool _initialized;

        /// <summary>Stable list of buffered remote player ids. Iterate by index to avoid alloc.</summary>
        public IReadOnlyList<string> PlayerIds => _playerIds;

        public void Configure(int tickRateHz)
        {
            // Two server frames of buffer: smooth, tolerant of one dropped packet, ~66ms at 30Hz.
            _interpolationDelayMs = 2.0 * (1000.0 / tickRateHz);
        }

        /// <summary>Appends one authoritative sample for a remote player.</summary>
        public void AddSample(string playerId, double serverStampMs, float x, float z, float yaw, float health)
        {
            if (!_tracks.TryGetValue(playerId, out var track))
            {
                track = new Track();
                _tracks[playerId] = track;
                _playerIds.Add(playerId);
            }

            Append(track, new Sample { TimeMs = serverStampMs, X = x, Z = z, Yaw = yaw, Health = health });

            if (serverStampMs > _latestServerMs) _latestServerMs = serverStampMs;
            if (!_initialized)
            {
                _renderClockMs = serverStampMs - _interpolationDelayMs;
                _initialized = true;
            }
        }

        public void Remove(string playerId)
        {
            if (_tracks.Remove(playerId))
                _playerIds.Remove(playerId);
        }

        /// <summary>Advances the render clock by real frame time, kept ~delay behind the newest sample.</summary>
        public void Advance(double deltaMs)
        {
            if (!_initialized) return;

            _renderClockMs += deltaMs;

            double target = _latestServerMs - _interpolationDelayMs;
            // Fell too far behind (long stall / packet burst) — snap forward to the target.
            if (_renderClockMs < target - _interpolationDelayMs)
                _renderClockMs = target;
            // Never render past the newest sample we have (no extrapolation into the future).
            if (_renderClockMs > _latestServerMs)
                _renderClockMs = _latestServerMs;
        }

        /// <summary>Interpolated transform for a remote player at the current render clock.</summary>
        public bool TryGet(string playerId, out Vector3 position, out float yaw, out float health)
        {
            position = default;
            yaw = 0f;
            health = 0f;

            if (!_tracks.TryGetValue(playerId, out var track) || track.Count == 0)
                return false;

            Sample oldest = Get(track, 0);
            Sample newest = Get(track, track.Count - 1);

            if (_renderClockMs <= oldest.TimeMs)
            {
                Write(oldest, out position, out yaw, out health);
                return true;
            }
            if (_renderClockMs >= newest.TimeMs)
            {
                Write(newest, out position, out yaw, out health);
                return true;
            }

            for (int i = 0; i < track.Count - 1; i++)
            {
                Sample a = Get(track, i);
                Sample b = Get(track, i + 1);
                if (_renderClockMs >= a.TimeMs && _renderClockMs <= b.TimeMs)
                {
                    double span = b.TimeMs - a.TimeMs;
                    float t = span > 1e-6 ? (float)((_renderClockMs - a.TimeMs) / span) : 0f;
                    position = new Vector3(Mathf.Lerp(a.X, b.X, t), 1f, Mathf.Lerp(a.Z, b.Z, t));
                    yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t);
                    health = Mathf.Lerp(a.Health, b.Health, t);
                    return true;
                }
            }

            Write(newest, out position, out yaw, out health);
            return true;
        }

        private static void Write(Sample s, out Vector3 position, out float yaw, out float health)
        {
            position = new Vector3(s.X, 1f, s.Z);
            yaw = s.Yaw;
            health = s.Health;
        }

        private static Sample Get(Track track, int index)
            => track.Buffer[(track.Head + index) % Capacity];

        private static void Append(Track track, Sample sample)
        {
            if (track.Count < Capacity)
            {
                track.Buffer[(track.Head + track.Count) % Capacity] = sample;
                track.Count++;
            }
            else
            {
                track.Buffer[track.Head] = sample;
                track.Head = (track.Head + 1) % Capacity;
            }
        }
    }
}
