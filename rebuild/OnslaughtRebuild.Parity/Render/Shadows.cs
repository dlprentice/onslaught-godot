// SPDX-License-Identifier: GPL-3.0-only
// What the shadow textures do to the game: which units are drawn into them each render, and so which of their effect
// emitters run. Adapted from onslaught-decomp src/DXShadows.cpp (CShadows::ResetShads 00552660, AddShadow 005526c0,
// UpdateTextures 00552740, AssignTextures 00552800, RenderTextures 005528b0) and shadows.h (RequestShadow 00477b70).

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Render;

/// <summary>
/// CShadows' render-to-texture shadows. The world pass asks for a shadow for each unit it reaches (the nearest by cue
/// distance are kept, one per texture); the next render's RenderTextures gives each asked-for unit a texture (its own
/// if it has one, else a free one, fading in), fades out the textures nobody asked for (freed below a fade of 2), and
/// draws each texture's unit into it from the light every other render (at once when newly assigned). Drawing a unit
/// is its Render, so its mesh moves to this frame's list and its effect emitters run. The landscape projection and the
/// textures' pixels draw nothing that affects the game.
/// </summary>
public sealed class Shadows
{
    private const float NoRequest = 16384.0f;

    private readonly (ActiveReader<Thing> Unit, float Dist)[] _requests;
    private readonly TexInfo[] _texInfo;
    private readonly TexInfo[] _free;
    private int _numFree;
    private ushort _fadeStep;
    private float _lastFadeTime;

    /// <param name="numTextures">16, or 32 in high detail mode (CLIPARAMS.mHiDetailMode, off unless asked for).</param>
    public Shadows(int numTextures = 16)
    {
        NumTextures = numTextures;
        _requests = new (ActiveReader<Thing>, float)[numTextures];
        _texInfo = new TexInfo[numTextures];
        _free = new TexInfo[numTextures];
        for (int i = 0; i < numTextures; i++)
        {
            _requests[i] = (new ActiveReader<Thing>(), 0.0f);
            _texInfo[i] = new TexInfo();
        }

        ResetShads();
    }

    public int NumTextures { get; }

    /// <summary>cg_Shadows: on in a single-player level (CDXLandscape's init), off in multiplayer.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The unit each texture holds (for checks), in texture order.</summary>
    public IEnumerable<Thing?> TextureUnits => _texInfo.Select(t => t.Unit.ToRead);

    /// <summary>ResetShads: before the world pass, nobody has asked yet.</summary>
    public void ResetShads()
    {
        if (Enabled)
        {
            foreach ((ActiveReader<Thing> unit, _) in _requests)
            {
                unit.SetReader(null);
            }

            for (int i = 0; i < NumTextures; i++)
            {
                _requests[i].Dist = NoRequest;
            }
        }
        else
        {
            _requests[NumTextures - 1].Dist = -1.0f;
        }
    }

    /// <summary>RequestShadow: kept when nearer than the farthest kept request.</summary>
    public void RequestShadow(Thing unit, float dist)
    {
        if (_requests[NumTextures - 1].Dist > dist)
        {
            AddShadow(unit, dist);
        }
    }

    /// <summary>AddShadow: an insertion into the requests, nearest first.</summary>
    private void AddShadow(Thing unit, float dist)
    {
        int i;
        for (i = 0; i < NumTextures; i++)
        {
            if (_requests[i].Dist > dist)
            {
                break;
            }
        }

        for (int j = NumTextures - 1; j > i; j--)
        {
            _requests[j].Dist = _requests[j - 1].Dist;
            _requests[j].Unit.SetReader(_requests[j - 1].Unit.ToRead);
        }

        _requests[i].Dist = dist;
        _requests[i].Unit.SetReader(unit);
    }

    /// <summary>
    /// RenderTextures: the fade step from the time since the last call, the textures' fades and owners, then each
    /// owned texture's unit drawn when its countdown reaches 0.
    /// </summary>
    public void RenderTextures(float now, Action<Thing> render)
    {
        _fadeStep = 1;
        if (now > _lastFadeTime)
        {
            float dt = now - _lastFadeTime;
            if (dt < 2.0f)
            {
                dt *= 20.0f;
                if (!(dt < 2.0f))
                {
                    _fadeStep = unchecked((ushort)X87.Fist(dt));
                }
            }
        }

        _lastFadeTime = now;
        if (!Enabled)
        {
            return;
        }

        UpdateTextures();
        AssignTextures();
        foreach (TexInfo t in _texInfo)
        {
            if (t.Unused == 1)
            {
                t.State = 1;
                t.Fade = unchecked((sbyte)(t.Fade - 2));
            }
        }

        foreach (TexInfo info in _texInfo)
        {
            if (info.Unit.ToRead is not { } unit)
            {
                continue;
            }

            if (info.Countdown == 0)
            {
                render(unit);
                info.Countdown = 2;
            }

            info.Countdown--;
        }
    }

    /// <summary>UpdateTextures: fades each owned texture in or out (freeing those faded out) and lists the free ones.</summary>
    private void UpdateTextures()
    {
        int f = 0;
        foreach (TexInfo t in _texInfo)
        {
            if (t.Unit.ToRead is null)
            {
                _free[f++] = t;
                continue;
            }

            t.Unused = 1;
            switch (t.State)
            {
                case 0:
                    t.Fade = 32;
                    break;
                case 1:
                    t.Fade = unchecked((sbyte)(t.Fade - 2));
                    if (t.Fade < 2)
                    {
                        t.Unit.SetReader(null);
                        _free[f++] = t;
                    }

                    break;
                case 2:
                    int fade = t.Fade + (_fadeStep * 2);
                    if (fade >= 32)
                    {
                        t.Fade = 32;
                        t.State = 0;
                    }
                    else
                    {
                        t.Fade = unchecked((sbyte)fade);
                    }

                    break;
            }
        }

        _numFree = f;
    }

    /// <summary>AssignTextures: each requesting unit keeps its texture (fading back in if it was fading out) or takes a free one.</summary>
    private void AssignTextures()
    {
        int f = 0;
        foreach ((ActiveReader<Thing> request, _) in _requests)
        {
            if (request.ToRead is not { } u)
            {
                continue;
            }

            bool found = false;
            foreach (TexInfo t in _texInfo)
            {
                if (ReferenceEquals(t.Unit.ToRead, u))
                {
                    t.Unused = 0;
                    if (t.State == 1)
                    {
                        t.State = 2;
                    }

                    found = true;
                    break;
                }
            }

            if (!found && _numFree > 0)
            {
                TexInfo t = _free[f];
                t.Fade = 2;
                t.State = 2;
                t.Unused = 0;
                t.Unit.SetReader(u);
                t.Countdown = 0;
                f++;
                _numFree--;
            }
        }
    }

    /// <summary>SShadowTexInfo's game-visible fields.</summary>
    private sealed class TexInfo
    {
        public ActiveReader<Thing> Unit { get; } = new();
        public sbyte Fade;
        public byte State;
        public byte Unused;
        public byte Countdown;
    }
}
