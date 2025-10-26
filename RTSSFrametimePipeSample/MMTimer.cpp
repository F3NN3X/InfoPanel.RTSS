// MMTimer.cpp: implementation of the CMMTimer class.
//
// created by Unwinder
//////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "MMTimer.h"
//////////////////////////////////////////////////////////////////////
#ifdef _DEBUG
#undef THIS_FILE
static char THIS_FILE[]=__FILE__;
#define new DEBUG_NEW
#endif
//////////////////////////////////////////////////////////////////////
#pragma warning (disable : 4201)
#include <mmsystem.h>
#pragma warning (default : 4201)
//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////
CMMTimer::CMMTimer()
{
	m_timerID		=	NULL;

	m_dwUser		=	0;
	m_hWnd			=	NULL;
	m_wParam		=	0;
	m_lParam		=	0;
	m_nMsg			=	0;
	m_nPeriod		=	0;
	m_nRes			=	0;
	m_pfnCallback	=	NULL;
		//invalidate member variables
	m_dwMode		=	MMTIMERMODE_INACTIVE;

}
//////////////////////////////////////////////////////////////////////
CMMTimer::~CMMTimer()
{
	Kill();
}
//////////////////////////////////////////////////////////////////////
BOOL CMMTimer::Create(UINT nPeriod, UINT nRes, DWORD dwUser, LPMMTIMERCALLBACK pfnCallback)
{
	Kill();

	m_nPeriod		= nPeriod;
	m_nRes			= nRes;
	m_dwUser		= dwUser;
	m_pfnCallback	= pfnCallback;
  
	if ((m_timerID = timeSetEvent (m_nPeriod, m_nRes, TimeProc, (DWORD)this, TIME_PERIODIC)) == NULL)
		return FALSE;

	m_dwMode = MMTIMERMODE_USERCALLBACK;

	return TRUE;
}
//////////////////////////////////////////////////////////////////////
BOOL CMMTimer::Create(UINT nPeriod, UINT nRes, DWORD dwUser, HWND hWnd, UINT nMsg, WPARAM wParam, LPARAM lParam)
{
	Kill();

	m_nPeriod		= nPeriod;
	m_nRes			= nRes;
	m_dwUser		= dwUser;
	m_hWnd			= hWnd;

	m_nMsg			= nMsg;
	m_wParam		= wParam;
	m_lParam		= lParam;
  
	if ((m_timerID = timeSetEvent (m_nPeriod, m_nRes, TimeProc, (DWORD)this, TIME_PERIODIC)) == NULL)
		return FALSE;

	m_dwMode = MMTIMERMODE_POSTMESSAGE;

	return TRUE;
}
//////////////////////////////////////////////////////////////////////
void CMMTimer::Kill(BOOL bEatMessages)
{
	if (m_timerID)
	{
		timeKillEvent(m_timerID);

		m_timerID = NULL;

		if (bEatMessages && (m_dwMode == MMTIMERMODE_POSTMESSAGE))
			EatMessages();
	}

	m_dwUser		=	0;
	m_hWnd			=	NULL;
	m_wParam		=	0;
	m_lParam		=	0;
	m_nMsg			=	0;
	m_nPeriod		=	0;
	m_nRes			=	0;
	m_pfnCallback	=	NULL;

	m_dwMode = MMTIMERMODE_INACTIVE;
	
}
//////////////////////////////////////////////////////////////////////
BOOL CMMTimer::IsActive()
{
	return (m_dwMode != MMTIMERMODE_INACTIVE);
}
//////////////////////////////////////////////////////////////////////
DWORD CMMTimer::GetMMTimerMode()
{
	return m_dwMode;
}
//////////////////////////////////////////////////////////////////////
void CMMTimer::PostMessageToTarget()
{
	::PostMessage(m_hWnd, m_nMsg, m_wParam, m_lParam);
}
//////////////////////////////////////////////////////////////////////
int CMMTimer::EatMessages()
{
	int eatCount = 0;

	if (IsActive())
	{
		MSG msg; 
		
		while (PeekMessage(&msg, m_hWnd, m_nMsg, m_nMsg, PM_REMOVE))
			eatCount++;
	}

	return eatCount;
}
//////////////////////////////////////////////////////////////////////
UINT CMMTimer::GetPeriod()
{
	return m_nPeriod;
}
//////////////////////////////////////////////////////////////////////
