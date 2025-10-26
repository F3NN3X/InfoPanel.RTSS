// RTSSFrametimePipeSampleDlg.cpp : implementation file
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "RTSSFrametimePipeSample.h"
#include "RTSSFrametimePipeSampleDlg.h"
#include "RTSSSharedMemory.h"
/////////////////////////////////////////////////////////////////////////////
#include <shlwapi.h>
#include <float.h>
#include <io.h>
/////////////////////////////////////////////////////////////////////////////
#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif
/////////////////////////////////////////////////////////////////////////////
// CAboutDlg dialog used for App About
/////////////////////////////////////////////////////////////////////////////
class CAboutDlg : public CDialog
{
public:
	CAboutDlg();

// Dialog Data
	//{{AFX_DATA(CAboutDlg)
	enum { IDD = IDD_ABOUTBOX };
	//}}AFX_DATA

	// ClassWizard generated virtual function overrides
	//{{AFX_VIRTUAL(CAboutDlg)
	protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV support
	//}}AFX_VIRTUAL

// Implementation
protected:
	//{{AFX_MSG(CAboutDlg)
	//}}AFX_MSG
	DECLARE_MESSAGE_MAP()
};
/////////////////////////////////////////////////////////////////////////////
CAboutDlg::CAboutDlg() : CDialog(CAboutDlg::IDD)
{
	//{{AFX_DATA_INIT(CAboutDlg)
	//}}AFX_DATA_INIT
}
/////////////////////////////////////////////////////////////////////////////
void CAboutDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	//{{AFX_DATA_MAP(CAboutDlg)
	//}}AFX_DATA_MAP
}
/////////////////////////////////////////////////////////////////////////////
BEGIN_MESSAGE_MAP(CAboutDlg, CDialog)
	//{{AFX_MSG_MAP(CAboutDlg)
		// No message handlers
	//}}AFX_MSG_MAP
END_MESSAGE_MAP()
/////////////////////////////////////////////////////////////////////////////
// CRTSSFrametimePipeSampleDlg dialog
/////////////////////////////////////////////////////////////////////////////
CRTSSFrametimePipeSampleDlg::CRTSSFrametimePipeSampleDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CRTSSFrametimePipeSampleDlg::IDD, pParent)
{
	//{{AFX_DATA_INIT(CRTSSFrametimePipeSampleDlg)
	//}}AFX_DATA_INIT
	// Note that LoadIcon does not require a subsequent DestroyIcon in Win32

	m_hIcon						= AfxGetApp()->LoadIcon(IDR_MAINFRAME);
	m_hPipe						= INVALID_HANDLE_VALUE;
	m_bPipeConnected			= FALSE;

	m_dwLogBufferTimestamp		= 0;

	m_dwFrame					= 0;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	//{{AFX_DATA_MAP(CRTSSFrametimePipeSampleDlg)
	//}}AFX_DATA_MAP
}
/////////////////////////////////////////////////////////////////////////////
BEGIN_MESSAGE_MAP(CRTSSFrametimePipeSampleDlg, CDialog)
	//{{AFX_MSG_MAP(CRTSSFrametimePipeSampleDlg)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	ON_WM_TIMER()
	ON_WM_DESTROY()
	//}}AFX_MSG_MAP
END_MESSAGE_MAP()
/////////////////////////////////////////////////////////////////////////////
// CRTSSFrametimePipeSampleDlg message handlers
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSFrametimePipeSampleDlg::OnInitDialog()
{
	CDialog::OnInitDialog();

	// Add "About..." menu item to system menu.

	// IDM_ABOUTBOX must be in the system command range.
	ASSERT((IDM_ABOUTBOX & 0xFFF0) == IDM_ABOUTBOX);
	ASSERT(IDM_ABOUTBOX < 0xF000);

	CMenu* pSysMenu = GetSystemMenu(FALSE);
	if (pSysMenu != NULL)
	{
		CString strAboutMenu;
		strAboutMenu.LoadString(IDS_ABOUTBOX);
		if (!strAboutMenu.IsEmpty())
		{
			pSysMenu->AppendMenu(MF_SEPARATOR);
			pSysMenu->AppendMenu(MF_STRING, IDM_ABOUTBOX, strAboutMenu);
		}
	}

	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon

	CWnd* pPlaceholder = GetDlgItem(IDC_PLACEHOLDER);

	if (pPlaceholder)
	{
		CRect rect; 
		pPlaceholder->GetClientRect(&rect);

		if (!m_graphWnd.CreateEx(0, AfxRegisterWndClass(CS_OWNDC|CS_DBLCLKS,::LoadCursor(NULL,IDC_ARROW)), "", WS_CHILD | WS_VISIBLE, rect,this, 0))
			return FALSE;
	}

	//create pipe

	CreatePipe();

	//init timer

	m_mmTimer.Create(33, 0, 0, m_hWnd, WM_TIMER, 0, 0); 

	AppendLog("", FALSE);

	Refresh();
	
	return TRUE;  // return TRUE  unless you set the focus to a control
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::OnSysCommand(UINT nID, LPARAM lParam)
{
	if ((nID & 0xFFF0) == IDM_ABOUTBOX)
	{
		CAboutDlg dlgAbout;
		dlgAbout.DoModal();
	}
	else
	{
		CDialog::OnSysCommand(nID, lParam);
	}
}
/////////////////////////////////////////////////////////////////////////////
// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::OnPaint() 
{
	if (IsIconic())
	{
		CPaintDC dc(this); // device context for painting

		SendMessage(WM_ICONERASEBKGND, (WPARAM) dc.GetSafeHdc(), 0);

		// Center icon in client rectangle
		int cxIcon = GetSystemMetrics(SM_CXICON);
		int cyIcon = GetSystemMetrics(SM_CYICON);
		CRect rect;
		GetClientRect(&rect);
		int x = (rect.Width() - cxIcon + 1) / 2;
		int y = (rect.Height() - cyIcon + 1) / 2;

		// Draw the icon
		dc.DrawIcon(x, y, m_hIcon);
	}
	else
	{
		CDialog::OnPaint();
	}
}
/////////////////////////////////////////////////////////////////////////////
// The system calls this to obtain the cursor to display while the user drags
//  the minimized window.
/////////////////////////////////////////////////////////////////////////////
HCURSOR CRTSSFrametimePipeSampleDlg::OnQueryDragIcon()
{
	return (HCURSOR) m_hIcon;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::OnTimer(UINT nIDEvent) 
{
	Refresh();

	m_mmTimer.EatMessages();
	
	CDialog::OnTimer(nIDEvent);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::OnDestroy() 
{
	m_mmTimer.Kill();

	FlushLogBuffer();

	DestroyPipe();

	CDialog::OnDestroy();	
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::Refresh()
{
	if (!IsPipeConnected())
		ConnectPipe();

	if (IsPipeConnected())
	{
		FRAMETIME_PIPE_DATA data;

		DWORD dwRead;

		while (ReadFile(m_hPipe, &data, sizeof(data), &dwRead, NULL))
		{
			if (!m_dwFrame)
				m_stats.Create(STATS_FPS_AVG_BUFFER_SIZE, STATS_FPS_LOW_BUFFER_SIZE);

			m_stats.AppendData(data.dwFrametime);

			DWORD		dwFrameCount				= m_stats.GetFrameCount();
			DWORD		dwTotalTime					= m_stats.GetTotalTime() / 1000;

			FLOAT		fltFramerateCur				= m_stats.GetCurFramerate();
			FLOAT		fltFramerateMin				= m_stats.GetMinFramerate();
			FLOAT		fltFramerateAvg				= m_stats.GetAvgFramerate();
			FLOAT		fltFramerateMax				= m_stats.GetMaxFramerate();
			FLOAT		fltFramerate1Dot0PercentLow	= m_stats.Get1Dot0PercentLowFramerate();
			FLOAT		fltFramerate0Dot1PercentLow	= m_stats.Get0Dot1PercentLowFramerate();

			CString strApp							= GetAppStr(data.dwApp);
			float	fltFrametime					= data.dwFrametime / 1000.0f;

			CString strCaption;
			strCaption.Format("%s, %d frames rendered in %.3f s\n%.1f cur | %.1f min | %.1f avg | %.1f max | %.1f 1%% low | %.1f 0.1%% low fps", strApp, dwFrameCount, dwTotalTime / 1000.0f, fltFramerateCur, fltFramerateMin, fltFramerateAvg, fltFramerateMax, fltFramerate1Dot0PercentLow, fltFramerate0Dot1PercentLow);

			m_graphWnd.SetCaption(strCaption);
			m_graphWnd.AppendData(fltFrametime);

			CString strLog;
			strLog.Format("%-8d, %-8.3f, %-8.1f, %-8.1f, %-8.1f, %-8.1f, %-8.1f, %-8.1f", m_dwFrame, fltFrametime, fltFramerateCur, fltFramerateMin, fltFramerateAvg, fltFramerateMax, fltFramerate1Dot0PercentLow, fltFramerate0Dot1PercentLow);

			if (!m_dwFrame)
			{
				AppendLogBuffer("Starting " + strApp, TRUE);

				strCaption.Format("%-8s  %-8s  %-8s  %-8s  %-8s  %-8s  %-8s  %-8s", "frame", "time", "cur", "min", "avg", "max", "1% low", "0.1% low");

				AppendLogBuffer(strCaption, FALSE);
			}

			AppendLogBuffer(strLog, FALSE);

			m_dwFrame++;
		}

		DWORD err = GetLastError();

		if (err == ERROR_BROKEN_PIPE)
			DisconnectPipe();
	}

	if (m_dwLogBufferTimestamp && ((GetTickCount() - m_dwLogBufferTimestamp) > LOG_BUFFER_TIMEOUT))
		FlushLogBuffer();

	m_graphWnd.RedrawWindow();
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSFrametimePipeSampleDlg::PreTranslateMessage(MSG* pMsg) 
{
	return CDialog::PreTranslateMessage(pMsg);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::CreatePipe()
{
	DestroyPipe();

	BYTE desc[SECURITY_DESCRIPTOR_MIN_LENGTH];
	InitializeSecurityDescriptor(&desc, SECURITY_DESCRIPTOR_REVISION);
	SetSecurityDescriptorDacl(&desc, TRUE, (PACL)NULL, FALSE);

	SECURITY_ATTRIBUTES attr;
	attr.nLength				= sizeof(attr);
	attr.lpSecurityDescriptor	= &desc;
	attr.bInheritHandle			= FALSE;

	m_hPipe = CreateNamedPipe("\\\\.\\PIPE\\RTSS_Frametime", PIPE_ACCESS_DUPLEX, PIPE_TYPE_MESSAGE | PIPE_NOWAIT, PIPE_UNLIMITED_INSTANCES, PIPE_BUFFER_SIZE, PIPE_BUFFER_SIZE, 0, &attr);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::DestroyPipe()
{
	DisconnectPipe();

	if (m_hPipe != INVALID_HANDLE_VALUE) 
		CloseHandle(m_hPipe);

	m_hPipe = INVALID_HANDLE_VALUE;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::ConnectPipe()
{
	if (m_hPipe != INVALID_HANDLE_VALUE) 
	{
		DisconnectPipe();

		if (ConnectNamedPipe(m_hPipe, NULL))
			m_bPipeConnected = TRUE;
		else
		{
			DWORD err = GetLastError();

			if (err == ERROR_PIPE_CONNECTED)
				m_bPipeConnected = TRUE;
		}
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::DisconnectPipe()
{
	if (m_bPipeConnected)
		DisconnectNamedPipe(m_hPipe);

	m_bPipeConnected = FALSE;

	m_dwFrame = 0;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSFrametimePipeSampleDlg::IsPipeConnected()
{
	return m_bPipeConnected;
}
/////////////////////////////////////////////////////////////////////////////
CString CRTSSFrametimePipeSampleDlg::GetAppStr(DWORD dwApp)
{
	CString result = "";

	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);

		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				if (dwApp < pMem->dwAppArrSize)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwApp * pMem->dwAppEntrySize);
	
					if (pEntry->dwProcessID)
					{
						char szName[MAX_PATH];
						strcpy_s(szName, sizeof(szName), pEntry->szName);
						PathStripPath(szName);

						result += szName;
						result += ", ";

						if (pMem->dwVersion >= 0x0002000A)
						{
							DWORD dwAPI = pEntry->dwFlags & APPFLAG_API_USAGE_MASK;

							if (dwAPI == APPFLAG_DD)
								result += "DirectDraw";

							if (dwAPI == APPFLAG_D3D8)
								result += "Direct3D8";

							if (dwAPI == APPFLAG_D3D9)
								result += "Direct3D9";

							if (dwAPI == APPFLAG_D3D9EX)
								result += "Direct3D9Ex";

							if (dwAPI == APPFLAG_D3D10)
								result += "Direct3D10";

							if (dwAPI == APPFLAG_D3D11)
								result += "Direct3D11";

							if (dwAPI == APPFLAG_D3D12)
								result += "Direct3D12";

							if (dwAPI == APPFLAG_D3D12AFR)
								result += "Direct3D12 AFR";

							if (dwAPI == APPFLAG_OGL)
								result += "OpenGL";

							if (dwAPI == APPFLAG_VULKAN)
								result += "Vulkan";

							if (pMem->dwVersion >= 0x0002000B)
							{
								if (pEntry->dwFlags & APPFLAG_ARCHITECTURE_X64)
								{
									result += ", ";
									result += "x64";
								}

								if (pEntry->dwFlags & APPFLAG_ARCHITECTURE_UWP)
								{
									result += ", ";
									result += "UWP";
								}
							}
						}
						else
						{
							if (pEntry->dwFlags & APPFLAG_DEPRECATED_DD)
								result += "DirectDraw";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_D3D8)
								result += "Direct3D8";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_D3D9)
								result += "Direct3D9";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_D3D9EX)
								result += "Direct3D9Ex";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_D3D10)
								result += "Direct3D10";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_D3D11)
								result += "Direct3D11";

							if (pEntry->dwFlags & APPFLAG_DEPRECATED_OGL)
								result += "OpenGL";
						}
					}
				}
			}

			UnmapViewOfFile(pMapAddr);
		}

		CloseHandle(hMapFile);
	}

	return result;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSFrametimePipeSampleDlg::AppendLog(LPCSTR lpLine, BOOL bAppend)
{
	if (m_strLogPath.IsEmpty())
	{
		char szLogPath[MAX_PATH];
		GetModuleFileName(NULL, szLogPath, sizeof(szLogPath));

		PathRenameExtension(szLogPath, ".log");

		m_strLogPath = szLogPath;
	}

	CStdioFile logFile;

	if (bAppend)
	{
		if (!logFile.Open(m_strLogPath, CFile::modeWrite|CFile::shareDenyWrite))
		{
			if (!logFile.Open(m_strLogPath, CFile::modeCreate|CFile::modeWrite|CFile::shareDenyWrite))
				return FALSE;
		}
	}
	else
		if (!logFile.Open(m_strLogPath, CFile::modeCreate|CFile::modeWrite|CFile::shareDenyWrite))
			return FALSE;

	logFile.SeekToEnd();

	TRY
	{
		logFile.WriteString(lpLine);
	}
	CATCH_ALL(exception)
	{
		return FALSE;
	}
	END_CATCH_ALL

	logFile.Close();
	logFile.Flush();

	return TRUE;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::AppendLogBuffer(LPCSTR lpLine, BOOL bAddTime)
{
	m_dwLogBufferTimestamp = GetTickCount();


	if (bAddTime)
	{
		SYSTEMTIME sysTime;
		GetLocalTime(&sysTime);

		CTime time(sysTime);
		CString strTime = time.Format("%d-%m-%Y %H:%M:%S, ");

		m_strLogBuffer += strTime;
	}

	m_strLogBuffer += lpLine;
	m_strLogBuffer += "\n";

	if (strlen(m_strLogBuffer) > LOG_BUFFER_SIZE)
		FlushLogBuffer();
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSFrametimePipeSampleDlg::FlushLogBuffer()
{
	m_dwLogBufferTimestamp = 0;

	if (strlen(m_strLogBuffer))
	{
		AppendLog(m_strLogBuffer, TRUE);

		m_strLogBuffer = "";
	}
}
/////////////////////////////////////////////////////////////////////////////

