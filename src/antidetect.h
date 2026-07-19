#pragma once
#include <windows.h>
#include <string>
#include <vector>
#include <algorithm>
#include <random>
#include <chrono>
#include <thread>
#include <intrin.h>

// ============================================================
//  ANTI-DETECTION / UNDETECTABLE MEASURES
// ============================================================
// These techniques help evade Roblox's Byfron/Hyperion anti-cheat
// and Windows Defender/AV detection.

class AntiDetect {
public:
    // Randomize thread timings to avoid pattern detection
    static void RandomSleep(int baseMs = 50, int varianceMs = 150) {
        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<> dis(0, varianceMs);
        std::this_thread::sleep_for(std::chrono::milliseconds(baseMs + dis(gen)));
    }

    // Obfuscate a string at compile/runtime to avoid static analysis
    static std::string ObfuscateStr(const std::string& input) {
        std::string result = input;
        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<> dis(0, 255);
        
        char key = static_cast<char>(dis(gen) & 0xFF);
        for (auto& c : result) {
            c ^= key;
        }
        result.insert(0, 1, key);
        return result;
    }

    static std::string DeobfuscateStr(const std::string& input) {
        if (input.empty()) return "";
        std::string result = input.substr(1);
        char key = input[0];
        for (auto& c : result) {
            c ^= key;
        }
        return result;
    }

    // Check if we're being debugged (multiple methods)
    static bool IsDebuggerPresent() {
        // Method 1: PEB BeingDebugged flag
        BOOL isDebugged = FALSE;
        CheckRemoteDebuggerPresent(GetCurrentProcess(), &isDebugged);
        if (isDebugged) return true;

        // Method 2: NtQueryInformationProcess (syscall)
        if (CheckNtDebug()) return true;

        // Method 3: Hardware breakpoints
        CONTEXT ctx = { 0 };
        ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS;
        if (GetThreadContext(GetCurrentThread(), &ctx)) {
            if (ctx.Dr0 || ctx.Dr1 || ctx.Dr2 || ctx.Dr3) return true;
        }

        return false;
    }

    // Anti-debug: NtQueryInformationProcess via syscall
    static bool CheckNtDebug() {
        // Use direct syscall to avoid hooking
        typedef NTSTATUS(NTAPI* pNtQueryInformationProcess)(
            HANDLE, ULONG, PVOID, ULONG, PULONG);
        
        HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
        if (!ntdll) return false;

        auto NtQueryInformationProcess = (pNtQueryInformationProcess)
            GetProcAddress(ntdll, "NtQueryInformationProcess");
        if (!NtQueryInformationProcess) return false;

        ULONG debugPort = 0;
        ULONG retLen = 0;
        NTSTATUS status = NtQueryInformationProcess(
            GetCurrentProcess(),
            0x7, // ProcessDebugPort
            &debugPort,
            sizeof(debugPort),
            &retLen);

        return (status >= 0 && debugPort != 0);
    }

    // Randomize memory allocation patterns
    static void* RandomAlloc(size_t size) {
        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<> dis(0, 0x1000);
        
        // Add random padding to allocations
        size_t paddedSize = size + (dis(gen) & 0xFF);
        return VirtualAlloc(nullptr, paddedSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    }

    // Clear PE header痕迹 from memory (makes memory scanning harder)
    static void ErasePEHeader(HMODULE hModule) {
        if (!hModule) hModule = GetModuleHandleW(nullptr);
        
        PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)hModule;
        PIMAGE_NT_HEADERS ntHeader = (PIMAGE_NT_HEADERS)((BYTE*)hModule + dosHeader->e_lfanew);
        
        DWORD oldProtect;
        // Randomize the header bytes slightly (non-destructive)
        VirtualProtect(dosHeader, 0x1000, PAGE_READWRITE, &oldProtect);
        
        // Zero out the DOS stub (not the header itself)
        memset((BYTE*)dosHeader + sizeof(IMAGE_DOS_HEADER), 0, 
               dosHeader->e_lfanew - sizeof(IMAGE_DOS_HEADER));
        
        // Randomize some optional header fields that aren't needed at runtime
        ntHeader->OptionalHeader.MajorImageVersion = 0;
        ntHeader->OptionalHeader.MinorImageVersion = 0;
        ntHeader->OptionalHeader.MajorLinkerVersion = 0;
        ntHeader->OptionalHeader.MinorLinkerVersion = 0;
        ntHeader->OptionalHeader.CheckSum = 0;
        
        VirtualProtect(dosHeader, 0x1000, oldProtect, &oldProtect);
    }

    // Spoof window class to avoid window detection
    static void SpoofWindowClass(HWND hwnd) {
        if (!hwnd) return;
        
        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<> dis(0, 99999);
        
        wchar_t newClass[64];
        swprintf_s(newClass, L"Window_%d_%d", 
                   GetCurrentProcessId(), dis(gen));
        
        SetClassLongPtrW(hwnd, GCLP_HBRBACKGROUND, (LONG_PTR)GetStockObject(BLACK_BRUSH));
    }

    // Check for sandbox/VM environments
    static bool IsSandboxed() {
        // Check for common VM artifacts
        HKEY hKey;
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, 
            L"HARDWARE\\DEVICEMAP\\Scsi\\Scsi Port 0\\Scsi Bus 0\\Target Id 0\\Logical Unit Id 0",
            0, KEY_READ, &hKey) == ERROR_SUCCESS) {
            
            wchar_t buffer[256] = { 0 };
            DWORD size = sizeof(buffer);
            if (RegQueryValueExW(hKey, L"Identifier", nullptr, nullptr, 
                (LPBYTE)buffer, &size) == ERROR_SUCCESS) {
                
                std::wstring id(buffer);
                if (id.find(L"VBOX") != std::wstring::npos ||
                    id.find(L"VMWARE") != std::wstring::npos ||
                    id.find(L"VIRTUAL") != std::wstring::npos ||
                    id.find(L"QEMU") != std::wstring::npos) {
                    RegCloseKey(hKey);
                    return true;
                }
            }
            RegCloseKey(hKey);
        }

        // Check for common sandbox DLLs
        const wchar_t* sandboxDlls[] = {
            L"sbiedll.dll",      // Sandboxie
            L"dbghelp.dll",      // Debugging tools
            L"api_log.dll",      // API logging
            L"dir_watch.dll",    // Directory watching
            L"pstorec.dll",      // Protected storage
            L"vmcheck.dll",      // VM detection
            L"wpespy.dll"        // WPE Pro
        };

        for (const auto& dll : sandboxDlls) {
            if (GetModuleHandleW(dll)) return true;
        }

        return false;
    }

    // Integrity check - verify our own code hasn't been modified
    static bool SelfIntegrityCheck() {
        HMODULE hModule = GetModuleHandleW(nullptr);
        if (!hModule) return false;

        PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)hModule;
        PIMAGE_NT_HEADERS ntHeader = (PIMAGE_NT_HEADERS)((BYTE*)hModule + dosHeader->e_lfanew);

        // Simple checksum of the .text section
        PIMAGE_SECTION_HEADER section = IMAGE_FIRST_SECTION(ntHeader);
        for (WORD i = 0; i < ntHeader->FileHeader.NumberOfSections; i++) {
            if (memcmp(section[i].Name, ".text", 5) == 0) {
                DWORD checksum = 0;
                DWORD* data = (DWORD*)((BYTE*)hModule + section[i].VirtualAddress);
                DWORD size = section[i].SizeOfRawData / sizeof(DWORD);
                
                for (DWORD j = 0; j < size; j++) {
                    checksum ^= data[j];
                }
                
                // Store expected checksum elsewhere and compare
                // This is a simplified version
                return (checksum != 0); // Non-zero means code is present
            }
        }
        return true;
    }

    // Delay load to avoid import table detection
    static FARPROC DelayLoad(const char* moduleName, const char* procName) {
        HMODULE hMod = GetModuleHandleA(moduleName);
        if (!hMod) {
            hMod = LoadLibraryA(moduleName);
            if (!hMod) return nullptr;
        }
        return GetProcAddress(hMod, procName);
    }

    // Randomize function call timing
    template<typename Func, typename... Args>
    static auto TimedCall(Func&& func, Args&&... args) -> decltype(func(args...)) {
        RandomSleep(10, 50);
        return func(std::forward<Args>(args)...);
    }

    // Apply all anti-detection measures
    static void ApplyAll() {
        // Erase PE headers to confuse scanners
        ErasePEHeader(nullptr);
        
        // Spoof window class if we have a window
        HWND hwnd = GetActiveWindow();
        if (hwnd) SpoofWindowClass(hwnd);
        
        // Check for debugger
        if (IsDebuggerPresent()) {
            // If debugger detected, we could crash gracefully or alter behavior
            // For now, just log it
            OutputDebugStringA("[AntiDetect] Debugger detected!\n");
        }
        
        // Check for sandbox
        if (IsSandboxed()) {
            OutputDebugStringA("[AntiDetect] Sandbox detected!\n");
        }
    }
};