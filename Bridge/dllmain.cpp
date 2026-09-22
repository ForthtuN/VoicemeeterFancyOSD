#include <string>
#include <Windows.h>
#include <Shellapi.h>

#define NETHOST_USE_AS_STATIC
#include "inc\hostfxr.h"
#include <vector>

#pragma warning(disable : 4996)

hostfxr_initialize_for_dotnet_command_line_fn init_cmdline;
hostfxr_close_fn close_fptr;
hostfxr_run_app_fn run_fptr;

//OUTPUT DLL IS "DXGI.dll"
//DO *NOT* CHANGE.

std::wstring GetExecutableDir()
{
    std::vector<WCHAR> buffer(512);
    for (;;)
    {
        DWORD len = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
        if (len == 0)
            return {};

        if (len < buffer.size() - 1)
        {
            std::wstring path(buffer.data(), len);
            auto separator = path.find_last_of(L"\\/");
            return separator == std::wstring::npos ? std::wstring{} : path.substr(0, separator);
        }

        buffer.resize(buffer.size() * 2);
    }
}

bool load_hostfxr()
{
    auto fxr_path = GetExecutableDir() + L"\\hostfxr.dll";

    HMODULE lib = LoadLibraryW(fxr_path.c_str());
    if (!lib)
        return false;

    init_cmdline = (hostfxr_initialize_for_dotnet_command_line_fn)GetProcAddress(lib, "hostfxr_initialize_for_dotnet_command_line");
    run_fptr = (hostfxr_run_app_fn)GetProcAddress(lib, "hostfxr_run_app");
    close_fptr = (hostfxr_close_fn)GetProcAddress(lib, "hostfxr_close");

    if (!(init_cmdline && run_fptr && close_fptr))
    {
        FreeLibrary(lib);
        init_cmdline = nullptr;
        run_fptr = nullptr;
        close_fptr = nullptr;
        return false;
    }

    return true;
}

HRESULT LoadCLR()
{
    auto host_path = GetExecutableDir() + L"\\";
    if (host_path == L"\\")
        return E_FAIL;

    auto exec_path = host_path + L"VoicemeeterFancyOsd.dll";

    if (!load_hostfxr())
    {
        // Nope, not necessary if you use Debug (x64) configuration.
        MessageBox(0, L"Framework-dependent net core? Then copy hostfxr.dll to the APPX output directory", L"Hey", 0);
        TerminateProcess(GetCurrentProcess(), EXIT_FAILURE);
        return E_FAIL;
    }

    hostfxr_initialize_parameters params{};
    params.dotnet_root = host_path.c_str();
    params.host_path = exec_path.c_str();
    params.size = sizeof(hostfxr_initialize_parameters);

    hostfxr_handle handle{};

    // The CoreCLR executes with empty arguments if there are no arguments.
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLine(), &argc);
    if (!argv)
    {
        TerminateProcess(GetCurrentProcess(), EXIT_FAILURE);
        return E_FAIL;
    }

    int32_t init_res;
    if (argc > 1)
    {
        std::vector<const char_t*> dotnet_args(argc + 1);

        dotnet_args[0] = exec_path.c_str(); // The 1st argument has to be the path of the main ModernFlyouts.dll (.NET) library
        for (int i = 0; i < argc; i++)
            dotnet_args[i + 1] = *(argv + i); // Subsequent arguments are passed after that

        init_res = init_cmdline(1 + argc, dotnet_args.data(), &params, &handle);
    }
    else
    {
        const char_t* dotnet_args[1] = { exec_path.c_str() };
        init_res = init_cmdline(1, dotnet_args, &params, &handle);
    }

    LocalFree(argv);

    bool isSuccess = (init_res >= 0) && (init_res <= 2);
    if (!isSuccess)
    {
        std::wstring message = L"Result code: " + std::to_wstring(init_res) +
            L". Check if the correct version of .NET is installed\nProgram may use different .NET version after update";
        MessageBox(0, message.c_str(), L"Error loading CLR", 0);
        if (handle)
            close_fptr(handle);
        TerminateProcess(GetCurrentProcess(), EXIT_FAILURE);
        return E_FAIL;
    }

    int32_t run_res = run_fptr(handle);
    close_fptr(handle);

    auto p = GetCurrentProcess();
    TerminateProcess(p, static_cast<UINT>(run_res));

    //It will never happen :)
    return S_OK;
}

extern "C"
{
    __declspec(dllexport) HRESULT CreateDXGIFactory(REFIID riid, void** ppFactory)
    {
        return LoadCLR();
    }

    __declspec(dllexport) HRESULT CreateDXGIFactory1(REFIID riid, void** ppFactory)
    {
        return LoadCLR();
    }

    //This is in case you get an error about entry point being meme for D3D11 (lol? d3d11 does not export CreateDXGIFactory*)
    __declspec(dllexport) HRESULT CreateDXGIFactory2(UINT Flags, REFIID riid, void** ppFactory)
    {
        return LoadCLR();
    }

    //Our main target.
    __declspec(dllexport) HRESULT DXGIDeclareAdapterRemovalSupport()
    {
        return LoadCLR();
    }
}

BOOL APIENTRY DllMain(HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved)
{
    return TRUE;
}
