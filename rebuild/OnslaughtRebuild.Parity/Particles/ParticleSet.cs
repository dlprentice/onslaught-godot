// SPDX-License-Identifier: GPL-3.0-or-later
// The loaded particle descriptors. Adapted from bea-decomp src/ParticleSet.cpp (Create 004cc020, FindByName 004cd7a0,
// ReadData 004cd7f0, LoadAllFromDisk 004cda60, LinkNode 004cdba0, UnlinkNode 004cdbe0) and DXParticleTexture.cpp (Get
// 0054fbc0).

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>
/// CParticleSet (PARTICLE_SET): the descriptors on a list sorted by name (ignoring case), and the list of those with
/// live particles in the order they gained one. Also the particle textures, newest first.
/// </summary>
public sealed class ParticleSet
{
    public const string InGameFile = @"data\ParticleSets\MainSet.par";
    public const string FrontEndFile = @"data\ParticleSets\Frontend.par";

    /// <summary>mList.</summary>
    public ParticleDescriptor? List;

    /// <summary>mFirstNode, mLastNode: the descriptors with live particles.</summary>
    public ParticleDescriptor? FirstNode, LastNode;

    /// <summary>CDXParticleTexture's list, newest first.</summary>
    public List<ParticleTexture> Textures { get; } = [];

    private ParticleDescriptor? _last;

    // sLink: where Create links the next descriptor, as the descriptor whose mNext it is (null: the list's head).
    private ParticleDescriptor? _linkOwner;

    public int Count { get; private set; }

    /// <summary>ReadData: the file's descriptors, then every reference by name resolved. FALSE when the file is cut short.</summary>
    public bool ReadData(byte[] file)
    {
        _last = null;
        List = null;
        Count = 0;
        var archive = new TokenArchive(file);
        int ignored = 0;
        float version = 0;
        string none = "";
        if (!archive.ReadToken(out Pst token, ref ignored, ref version, ref none) && token != Pst.FileHeader)
        {
            return false;
        }

        if (!archive.ReadToken(out token, ref ignored, ref version, ref none) && token != Pst.FileVersion)
        {
            return false;
        }

        int count = 0;
        if (!archive.ReadToken(out token, ref count, ref version, ref none) && token != Pst.NumParticleDescriptors)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            int type = 0;
            string name = "";
            if ((!archive.ReadToken(out token, ref type, ref version, ref none) && token != Pst.ParticleDescriptorType)
                || (!archive.ReadToken(out token, ref ignored, ref version, ref name) && token != Pst.ParticleDescriptorName))
            {
                return false;
            }

            ParticleDescriptor? pd = Create(name, (PdType)type, true);
            if (pd is null)
            {
                return false;
            }

            pd.Load(archive, this);
        }

        archive.HandleRelocation(this);
        return true;
    }

    /// <summary>
    /// Create: a new descriptor of that type linked in name order; with <paramref name="append"/>, straight after the last
    /// one when that is the list's tail and the name sorts after it. Null when the name is taken.
    /// </summary>
    public ParticleDescriptor? Create(string name, PdType type, bool append)
    {
        if (append && _last is { Next: null } last && Stricmp(last.Name, name) < 0)
        {
            _linkOwner = last;
        }
        else if (FindByName(name) is not null)
        {
            return null;
        }

        ParticleDescriptor pd = type switch
        {
            PdType.SimpleSprite => new PdSimpleSprite(),
            PdType.Emitter => new PdEmitter(),
            PdType.Modifier => new PdModifier(),
            PdType.Selector => new PdSelector(),
            PdType.ColourRange => new PdColourRange(),
            PdType.Timeline => new PdTimeline(),
            PdType.Shape => new PdShape(),
            PdType.Trail => new PdTrail(),
            PdType.Mover => new PdMover(),
            PdType.Function => new PdFunction(),
            PdType.Mesh => new PdMesh(),
            PdType.FoR => new PdFoR(),
            PdType.PMesh => new PdPMesh(),
            _ => throw new NotSupportedException($"particle descriptor type {(int)type} (retail leaves the result unset)"),
        };
        pd.Name = name.Length > 49 ? name[..49] : name;
        if (_linkOwner is null)
        {
            pd.Next = List;
            List = pd;
        }
        else
        {
            pd.Next = _linkOwner.Next;
            _linkOwner.Next = pd;
        }

        _last = pd;
        Count++;
        return pd;
    }

    /// <summary>FindByName: the descriptor of that name, ignoring case; leaves the link at its place in the order.</summary>
    public ParticleDescriptor? FindByName(string name)
    {
        _linkOwner = null;
        for (ParticleDescriptor? pd = List; pd is not null;)
        {
            int c = Stricmp(name, pd.Name);
            if (c == 0)
            {
                return pd;
            }

            if (c < 0)
            {
                return null;
            }

            _linkOwner = pd;
            pd = pd.Next;
        }

        return null;
    }

    /// <summary>GetPD: FindByName.</summary>
    public ParticleDescriptor? GetPD(string name) => FindByName(name);

    /// <summary>HandleRelocation's lookup (bsearch over the sorted list); none for no match.</summary>
    internal ParticleDescriptor? Lookup(string name)
    {
        for (ParticleDescriptor? pd = List; pd is not null; pd = pd.Next)
        {
            int c = Stricmp(name, pd.Name);
            if (c == 0)
            {
                return pd;
            }

            if (c < 0)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>CDXParticleTexture::Get: the texture of that name (ignoring case) and mode, made the first time.</summary>
    public ParticleTexture GetTexture(string name, int mode)
    {
        foreach (ParticleTexture t in Textures)
        {
            if (Stricmp(name, t.Name) == 0 && t.Mode == mode)
            {
                return t;
            }
        }

        var made = new ParticleTexture(name, mode);
        Textures.Insert(0, made);
        return made;
    }

    /// <summary>LinkNode: onto the tail of the live list.</summary>
    public void LinkNode(ParticleDescriptor node)
    {
        if (LastNode is null)
        {
            LastNode = FirstNode = node;
            node.NodePrev = null;
            node.NodeNext = null;
        }
        else
        {
            LastNode.NodeNext = node;
            node.NodePrev = LastNode;
            LastNode = node;
        }
    }

    /// <summary>UnlinkNode.</summary>
    public void UnlinkNode(ParticleDescriptor node)
    {
        if (node.NodePrev is null)
        {
            FirstNode = node.NodeNext;
        }
        else
        {
            node.NodePrev.NodeNext = node.NodeNext;
        }

        if (node.NodeNext is null)
        {
            LastNode = node.NodePrev;
        }
        else
        {
            node.NodeNext.NodePrev = node.NodePrev;
        }

        node.NodePrev = null;
        node.NodeNext = null;
    }

    /// <summary>The C runtime's _stricmp: bytes compared with A..Z folded to lower case.</summary>
    public static int Stricmp(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            int ca = Fold(a[i]);
            int cb = Fold(b[i]);
            if (ca != cb)
            {
                return ca - cb;
            }
        }

        return a.Length - b.Length;

        static int Fold(char c) => c is >= 'A' and <= 'Z' ? c + 32 : c;
    }
}
