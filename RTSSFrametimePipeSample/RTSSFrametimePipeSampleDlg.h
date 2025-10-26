// RTSSFrametimePipeSampleDlg.h : header file
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#ifndef _RTSSFRAMETIMEPIPESAMPLEDLG_H_INCLUDED_
#define _RTSSFRAMETIMEPIPESAMPLEDLG_H_INCLUDED_
/////////////////////////////////////////////////////////////////////////////
#include "MMTimer.h"
#include "GraphWnd.h"
#include "FrametimeStats.h"
/////////////////////////////////////////////////////////////////////////////
#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000
/////////////////////////////////////////////////////////////////////////////
typedef struct FRAMETIME_PIPE_DATA
{
	DWORD	dwApp;
	DWORD	dwFrametime;
} FRAMETIME_PIPE_DATA, *LPFRAMETIME_PIPE_DATA;
/////////////////////////////////////////////////////////////////////////////
#define PIPE_BUFFER_SIZE	(4096 * sizeof(FRAMETIME_PIPE_DATA))
/////////////////////////////////////////////////////////////////////////////
#define LOG_BUFFER_SIZE		65536
#define LOG_BUFFER_TIMEOUT	1000
/////////////////////////////////////////////////////////////////////////////
class CRTSSFrametimePipeSampleDlg : public CDialog
{
// Construction
public:
	CRTSSFrametimePipeSampleDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	//{{AFX_DATA(CRTSSFrametimePipeSampleDlg)
	enum { IDD = IDD_RTSSFRAMETIMEPIPESAMPLE_DIALOG };
	//}}AFX_DATA

	// ClassWizard generated virtual function overrides
	//{{AFX_VIRTUAL(CRTSSFrametimePipeSampleDlg)
	public:
	virtual BOOL PreTranslateMessage(MSG* pMsg);
	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support
	//}}AFX_VIRTUAL

// Implementation
protected:
	void Refresh();
	void CreatePipe();
	void DestroyPipe();
	void ConnectPipe();
	void DisconnectPipe();
	BOOL IsPipeConnected();

	BOOL AppendLog(LPCSTR lpLine, BOOL bAppend);
	void AppendLogBuffer(LPCSTR lpLine, BOOL bAddTime);
	void FlushLogBuffer();

	CString GetAppStr(DWORD dwApp);
	
	HICON						m_hIcon;

	CGraphWnd					m_graphWnd;
	CMMTimer					m_mmTimer;

	CString						m_strLogPath;
	CString						m_strLogBuffer;
	DWORD						m_dwLogBufferTimestamp;

	HANDLE						m_hPipe;
	BOOL						m_bPipeConnected;

	DWORD						m_dwFrame;

	CFrametimeStats				m_stats;
	
	// Generated message map functions
	//{{AFX_MSG(CRTSSFrametimePipeSampleDlg)
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	afx_msg void OnTimer(UINT nIDEvent);
	afx_msg void OnDestroy();
	//}}AFX_MSG
	DECLARE_MESSAGE_MAP()
};
/////////////////////////////////////////////////////////////////////////////
//{{AFX_INSERT_LOCATION}}
// Microsoft Visual C++ will insert additional declarations immediately before the previous line.
/////////////////////////////////////////////////////////////////////////////
#endif
/////////////////////////////////////////////////////////////////////////////
