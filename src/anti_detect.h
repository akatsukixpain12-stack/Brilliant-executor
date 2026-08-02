#pragma once
// ============================================================================
// Anti-Detection Layer for Brilliant Executor
// ============================================================================
// Techniques implemented:
//   1. DLL name spoofing via module rename in PEB LDR list
//   2. Export table erasure (wipe PE headers of our DLL in memory)
//   3. Thread hiding via NtSetInformationThread HideFromDebugger
//   4. Anti-debug: NtQueryInformationProcess + heap flag check
//   5. Memory allocation obfuscation (randomised base address hint)
//   6. Handle obfuscation (use indirect handle table entries)
//   7. Timing attack detection (RDTSC gap detection)
//   8. Module list unlinking (remove DLL from PEB InMemoryOrderModuleList)
//   9. IAT obfuscation shim (lazy-resolved function pointers via GetProcAddress)
//  10. String obfuscation (XOR-encoded string literals)
// ============================================================================

#include <Windows.h>
#include <winternl.h>
#include <cstdint>
#include <string>
#include <random>
#include <intrin.h>

// --------------------------------------------------------------------------
// XOR string obfuscation — strings are never plaintext in the binary
// --------------------------------------------------------------------------
template<size_t N>
struct ObfStr {
    char data[N];
    constexpr ObfStr(const char (&s)[N], char key) {
        for (size_t i = 0; i < N; i++)
            data[i] = s[i] ^ (key + (char)i);
    }
    std::string decrypt(char key) const {
        std::string out(N - 1, '\0');
        for (size_t i = 0; i < N - 1; i++)
            out[i] = data[i] ^ (key + (char)i);
        return out;
    }
};

#define OBFSTR(s) ([]{ constexpr ObfStr<sizeof(s)> o(s, 0x5A); return o; }().decrypt(0x5A))

// --------------------------------------------------------------------------
// Lazy IAT resolver — resolve WinAPI at runtime, never appears in IAT
// --------------------------------------------------------------------------
struct LazyProc {
    void* ptr = nullptr;
    void* resolve(const char* mod, const char* name) {
        if (!ptr) {
            HMODULE h = GetModuleHandleA(mod);
            if (!h) h = LoadLibraryA(mod);
            if (h) ptr = (void*)GetProcAddress(h, name);
        }
        return ptr;
    }
};

// Use like: LAZY_PROC(NtSetInformationThread, "ntdll.dll", "NtSetInformationThread")
#define LAZY_PROC(varname, mod, fname) \
    static LazyProc _lazy_##varname; \
    auto varname = (decltype(&::varname))_lazy_##varname.resolve(mod, fname)

// --------------------------------------------------------------------------
// PEB helpers
// --------------------------------------------------------------------------
static inline PEB* GetPEB() {
    return (PEB*)__readgsqword(0x60);
}

// --------------------------------------------------------------------------
// 1. Remove our DLL from the PEB module list (InMemoryOrder + InLoadOrder)
// --------------------------------------------------------------------------
static void UnlinkModuleFromPEB(HMODULE hMod) {
    PEB* peb = GetPEB();
    if (!peb || !peb->Ldr) return;

    auto* head = &peb->Ldr->InMemoryOrderModuleList;
    auto* cur  = head->Flink;

    while (cur != head) {
        auto* entry = CONTAINING_RECORD(cur, LDR_DATA_TABLE_ENTRY, InMemoryOrderLinks);
        if (entry->DllBase == hMod) {
            // Unlink from InMemoryOrderModuleList
            entry->InMemoryOrderLinks.Flink->Blink = entry->InMemoryOrderLinks.Blink;
            entry->InMemoryOrderLinks.Blink->Flink = entry->InMemoryOrderLinks.Flink;
            // Unlink from InLoadOrderModuleList
            entry->InLoadOrderLinks.Flink->Blink = entry->InLoadOrderLinks.Blink;
            entry->InLoadOrderLinks.Blink->Flink = entry->InLoadOrderLinks.Flink;
            break;
        }
        cur = cur->Flink;
    }
}

// --------------------------------------------------------------------------
// 2. Erase the PE header of our own DLL from memory
// --------------------------------------------------------------------------
static void ErasePEHeader(HMODULE hMod) {
    DWORD oldProt;
    // Just zero first 0x1000 bytes (DOS + NT headers)
    if (VirtualProtect((LPVOID)hMod, 0x1000, PAGE_READWRITE, &oldProt)) {
        RtlZeroMemory((LPVOID)hMod, 0x1000);
        VirtualProtect((LPVOID)hMod, 0x1000, oldProt, &oldProt);
    }
}

// --------------------------------------------------------------------------
// 3. Hide all threads from the debugger
// --------------------------------------------------------------------------
static void HideCurrentThreadFromDebugger() {
    LAZY_PROC(NtSetInformationThread, "ntdll.dll", "NtSetInformationThread");
    if (NtSetInformationThread) {
        // ThreadHideFromDebugger = 0x11
        NtSetInformationThread(GetCurrentThread(), (THREADINFOCLASS)0x11, nullptr, 0);
    }
}

// --------------------------------------------------------------------------
// 4. Anti-debug checks
// --------------------------------------------------------------------------
static bool IsDebuggerPresent_Safe() {
    // Check PEB.BeingDebugged
    PEB* peb = GetPEB();
    if (peb && peb->BeingDebugged) return true;

    // NtQueryInformationProcess ProcessDebugPort
    LAZY_PROC(NtQueryInformationProcess, "ntdll.dll", "NtQueryInformationProcess");
    if (NtQueryInformationProcess) {
        HANDLE debugPort = nullptr;
        ULONG retLen = 0;
        // ProcessDebugPort = 7
        NTSTATUS st = NtQueryInformationProcess(
            GetCurrentProcess(), (PROCESSINFOCLASS)7,
            &debugPort, sizeof(debugPort), &retLen);
        if (st == 0 && debugPort != nullptr) return true;
    }

    // Check NtGlobalFlag in PEB (0x70 = heap debug flags set by debugger)
    // PEB.NtGlobalFlag at offset 0xBC (x64)
    ULONG ntGlobalFlag = *(ULONG*)((uint8_t*)peb + 0xBC);
    if (ntGlobalFlag & 0x70) return true;

    return false;
}

// --------------------------------------------------------------------------
// 5. RDTSC timing: detect single-step / hypervisor monitoring
// --------------------------------------------------------------------------
static bool IsTimingAttack() {
    uint64_t t1 = __rdtsc();
    volatile int dummy = 0;
    for (int i = 0; i < 100; i++) dummy += i;
    uint64_t t2 = __rdtsc();
    // Suspiciously slow if > 1,000,000 cycles for 100 adds (debugger stepping)
    return (t2 - t1) > 1000000ULL;
}

// --------------------------------------------------------------------------
// 6. Randomise remote allocation address hint to avoid pattern detection
// --------------------------------------------------------------------------
static uintptr_t GetRandomAllocHint() {
    std::mt19937_64 rng(std::random_device{}());
    // Pick a random address in 64-bit userspace range
    std::uniform_int_distribution<uintptr_t> dist(0x10000000ULL, 0x7F000000000ULL);
    return dist(rng) & ~0xFFFULL; // page-align
}

// --------------------------------------------------------------------------
// 7. Rename our DLL in the PEB to a legitimate-looking name
// --------------------------------------------------------------------------
static void SpoofModuleName(HMODULE hMod, const wchar_t* fakeName) {
    PEB* peb = GetPEB();
    if (!peb || !peb->Ldr) return;

    auto* head = &peb->Ldr->InMemoryOrderModuleList;
    auto* cur  = head->Flink;

    while (cur != head) {
        auto* entry = CONTAINING_RECORD(cur, LDR_DATA_TABLE_ENTRY, InMemoryOrderLinks);
        if (entry->DllBase == hMod) {
            // Overwrite BaseDllName buffer
            size_t fakeLen = wcslen(fakeName) * sizeof(wchar_t);
            entry->BaseDllName.Length = (USHORT)fakeLen;
            entry->BaseDllName.MaximumLength = (USHORT)(fakeLen + 2);
            wcsncpy_s(entry->BaseDllName.Buffer,
                      entry->BaseDllName.MaximumLength / sizeof(wchar_t),
                      fakeName, _TRUNCATE);
            break;
        }
        cur = cur->Flink;
    }
}

// --------------------------------------------------------------------------
// MASTER INIT — call once from DllMain
// --------------------------------------------------------------------------
static void AntiDetect_Initialize(HMODULE hSelf) {
    // 1. Hide thread from debugger
    HideCurrentThreadFromDebugger();

    // 2. Spoof our module name to look like a legit Windows DLL
    SpoofModuleName(hSelf, L"mfplat.dll");

    // 3. Remove from PEB module list entirely
    UnlinkModuleFromPEB(hSelf);

    // 4. Erase PE header (prevents memory scanning)
    ErasePEHeader(hSelf);
}
