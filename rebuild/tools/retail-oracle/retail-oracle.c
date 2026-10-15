// retail-oracle.c: runs retail BEA.exe maths functions natively (32-bit Linux) as an oracle for the parity core.
// The memory image (mkimage.py) is mapped at the image base 0x400000 (no relocation needed), the FPU control word
// is set to 0x007F (24-bit precision, as the game runs), and each case calls the retail function by address.
// Input lines: "<function> <hex words...>"; output lines: "<function> <hex words...>" (floats as IEEE bits).
// Build: gcc -m32 -O1 -no-pie -o retail-oracle retail-oracle.c
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <sys/mman.h>

#define BASE 0x400000u

typedef struct { float X, Y, Z, Pad; } FVector;			// maths.h's FVector (16 bytes)

typedef struct								// GeneralVolume.h's CCollisionReport (0xe8 bytes)
{
	int32_t		mUnknown00;
	uint8_t		mUnknown04[4];
	FVector		mNormals[6];
	int32_t		mContactParts[6];
	int32_t		mNumContacts;
	FVector		mContactPos;
	int32_t		mUnknown94;
	FVector		mRelativeMove;
	int32_t		mUnknownA8;
	float		mContactDistances[6];
	float		mDistance;
	int32_t		mStuck;
	int32_t		mTestTriangles;
	int32_t		mUnknownD0;
	FVector		mTriangleNormal;
	void		*mPart;
} Report;
_Static_assert(sizeof(Report) == 0xe8, "report size");

static uint16_t sControlWord = 0x007f;

static void SetCw(uint16_t cw) { __asm__ volatile("fldcw %0" : : "m"(cw)); }
static uint16_t GetCw(void) { uint16_t cw; __asm__ volatile("fnstcw %0" : "=m"(cw)); return cw; }

static uint32_t W(float f) { uint32_t u; memcpy(&u, &f, 4); return u; }
static float F(uint32_t u) { float f; memcpy(&f, &u, 4); return f; }

static int ReadWords(char *s, uint32_t *w, int max)
{
	int n = 0;
	char *tok = strtok(s, " \t\n");
	while (tok && n < max)
	{
		w[n++] = (uint32_t)strtoul(tok, NULL, 16);
		tok = strtok(NULL, " \t\n");
	}
	return n;
}

static FVector V(const uint32_t *w) { FVector v = { F(w[0]), F(w[1]), F(w[2]), 0 }; return v; }

static void PrintV(const FVector *v) { printf(" %08x %08x %08x", W(v->X), W(v->Y), W(v->Z)); }

static void PrintReport(const Report *r)
{
	printf(" %08x %08x %08x %08x", (uint32_t)r->mUnknownA8, (uint32_t)r->mUnknown94, W(r->mDistance), (uint32_t)r->mStuck);
	PrintV(&r->mContactPos);
	PrintV(&r->mTriangleNormal);
}

// The report state a case starts from: A8, distance, test-triangles, D0.
static void InitReport(Report *r, const uint32_t *w)
{
	memset(r, 0, sizeof(*r));
	r->mUnknownA8 = (int32_t)w[0];
	r->mDistance = F(w[1]);
	r->mTestTriangles = (int32_t)w[2];
	r->mUnknownD0 = (int32_t)w[3];
}

typedef int (*SegTriFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *, Report *);
typedef int (*SweptTriFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *, float, Report *);
typedef int (*PrismFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *);
typedef void *(*ClosestFn)(FVector *, const FVector *, const FVector *, const FVector *, const FVector *);
typedef float (*RayFn)(const FVector *, const FVector *, float);
typedef float (*BoxFn)(const FVector *, const FVector *);
typedef unsigned char (*ClipFn)(float *, float *, float *, float *, float *, float *, const float *);

int main(int argc, char **argv)
{
	const char *image = argc > 1 ? argv[1] : "bea-image.bin";
	if (argc > 2)
		sControlWord = (uint16_t)strtoul(argv[2], NULL, 16);
	FILE *f = fopen(image, "rb");
	if (!f) { perror(image); return 2; }
	fseek(f, 0, SEEK_END);
	long size = ftell(f);
	fseek(f, 0, SEEK_SET);
	void *mem = mmap((void *)BASE, (size_t)size, PROT_READ | PROT_WRITE | PROT_EXEC,
		MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED_NOREPLACE, -1, 0);
	if (mem != (void *)BASE) { perror("mmap"); return 2; }
	if (fread(mem, 1, (size_t)size, f) != (size_t)size) { perror("read"); return 2; }
	fclose(f);

	char line[4096];
	uint32_t w[256];
	while (fgets(line, sizeof line, stdin))
	{
		char name[64];
		if (sscanf(line, "%63s", name) != 1)
			continue;
		int n = ReadWords(line + strlen(name), w, 256);
		uint16_t saved = GetCw();
		printf("%s", name);
		if (!strcmp(name, "TestSegmentAgainstTriangle") && n >= 19)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), s = V(w + 9), e = V(w + 12);
			Report r; InitReport(&r, w + 15);
			SetCw(sControlWord);
			int hit = ((SegTriFn)0x00478c20)(&a, &b, &c, &s, &e, &r);
			SetCw(saved);
			printf(" %08x", (uint32_t)hit);
			PrintReport(&r);
		}
		else if (!strcmp(name, "TestSweptSphereAgainstTriangle") && n >= 20)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), p = V(w + 9), m = V(w + 12);
			float radius = F(w[15]);
			Report r; InitReport(&r, w + 16);
			SetCw(sControlWord);
			int hit = ((SweptTriFn)0x00478510)(&a, &b, &c, &p, &m, radius, &r);
			SetCw(saved);
			printf(" %08x", (uint32_t)hit);
			PrintReport(&r);
		}
		else if (!strcmp(name, "PointInsideTrianglePrism") && n >= 15)
		{
			FVector p = V(w), a = V(w + 3), b = V(w + 6), c = V(w + 9), nrm = V(w + 12);
			SetCw(sControlWord);
			int in = ((PrismFn)0x00479020)(&p, &a, &b, &c, &nrm);
			SetCw(saved);
			printf(" %08x", (uint32_t)in);
		}
		else if (!strcmp(name, "SelectClosestPointOnTriangleEdges") && n >= 12)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), p = V(w + 9), out = { 0, 0, 0, 0 };
			SetCw(sControlWord);
			((ClosestFn)0x00479200)(&out, &a, &b, &c, &p);
			SetCw(saved);
			PrintV(&out);
		}
		else if (!strcmp(name, "RaySphereEntryDistance") && n >= 7)
		{
			FVector s = V(w), e = V(w + 3);
			SetCw(sControlWord);
			float d = ((RayFn)0x00479630)(&s, &e, F(w[6]));
			SetCw(saved);
			printf(" %08x", W(d));
		}
		else if (!strcmp(name, "DistanceOutsideBox") && n >= 6)
		{
			FVector p = V(w), x = V(w + 3);
			SetCw(sControlWord);
			float d = ((BoxFn)0x00479770)(&p, &x);
			SetCw(saved);
			printf(" %08x", W(d));
		}
		else if (!strcmp(name, "ClipSegmentAgainstAABB3D") && n >= 12)
		{
			float v[6], bounds[6];
			for (int i = 0; i < 6; i++) { v[i] = F(w[i]); bounds[i] = F(w[6 + i]); }
			SetCw(sControlWord);
			unsigned char ok = ((ClipFn)0x00478160)(&v[0], &v[1], &v[2], &v[3], &v[4], &v[5], bounds);
			SetCw(saved);
			printf(" %08x", (uint32_t)ok);
			for (int i = 0; i < 6; i++) printf(" %08x", W(v[i]));
		}
		else
			printf(" ERROR unknown function or too few words (%d)", n);
		printf("\n");
	}
	return 0;
}
