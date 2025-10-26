// MMTimer.h: interface for the CMMTimer class.
//
// created by Unwinder
//////////////////////////////////////////////////////////////////////
#ifndef _MMTIMER_H_INCLUDED_
#define _MMTIMER_H_INCLUDED_
//////////////////////////////////////////////////////////////////////
#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000
//////////////////////////////////////////////////////////////////////
typedef void (*LPMMTIMERCALLBACK) (DWORD dwUserData);
//////////////////////////////////////////////////////////////////////
#define MMTIMERMODE_INACTIVE		0
#define MMTIMERMODE_USERCALLBACK	1
#define MMTIMERMODE_POSTMESSAGE		2
//////////////////////////////////////////////////////////////////////
class  CMMTimer  
{
public:
	UINT GetPeriod();

	//creation/destruction methods

	BOOL Create (UINT nPeriod, UINT nRes, DWORD dwUser, LPMMTIMERCALLBACK pfnCallback);
		//Create multimedia timer in MMTIMERMODE_USERCALLBACK mode.
		//In this mode u must supply ptr to your own callback function which will'be called
		//from multimedia timer's callback function.

		//e.g u can use this method in this way:

		//	BOOL CMyWnd::InitMultimediaTimer()
		//	{
		//		...
		//		m_mmtimer.Create(100,0,(DWORD)this,MyCallback);
		//		...
		//	}
		//		...
		//	void CMyWnd::MyCallback(DWORD dwUser)
		//		//must be declared as STATIC
		//	{
		//		CMyWnd* pThis = (CMyWnd*)dwUser;
		//		
		//		pThis->DoSomething();
		//	}
	BOOL Create (UINT nPeriod, UINT nRes, DWORD dwUser, HWND hWnd, UINT nMsg, WPARAM wParam, LPARAM lParam);
		//Create multimedia timer in MMTIMERMODE_POSTMESSAGE mode.
		//In this mode u must supply window handle and message which will be posted to
		//it from miltimedia timer's callback.

		//!!!Don't forget to call EatMessage method from message handler. It will eat
		//extra messages posted from multimedia timer's callback
	void Kill(BOOL bEatMessages = TRUE);
		//Kill multimedia timer. If multimedia timer work in MMTIMERMODE_POSTMESSAGE mode
		//and bEatMessages == TRUE, this method will call EatMessages method
	

	//info methods

	DWORD GetMMTimerMode();
		//Return multimedia timer mode (MMTIMERMODE_XXXX constant).
	BOOL IsActive();
		//return multimedia timer activation state

	//additional methods

	int EatMessages();
		//eat extra messages posted from multimedia timer's callback
		//return number of eaten messages

	CMMTimer();
	virtual ~CMMTimer();

protected:

	void PostMessageToTarget();
		//internal helper function

	static void CALLBACK TimeProc(UINT /*uID*/, UINT /*uMsg*/, DWORD dwUser, DWORD /*dw1*/, DWORD /*dw2*/)
		//multimedia timer callback
	{
		CMMTimer * pThis = (CMMTimer*) dwUser;

		switch (pThis->GetMMTimerMode())
		{
		case MMTIMERMODE_USERCALLBACK:
			(pThis->m_pfnCallback) (pThis->m_dwUser);
			break;
		case MMTIMERMODE_POSTMESSAGE:
			pThis->PostMessageToTarget();
			break;
		}
	};

	//common member variables

	DWORD m_dwMode;
		//multimedia timer mode (MMTIMERMODE_XXX constants)

	UINT m_timerID;
		//multimedia timer ID
	DWORD m_dwUser;	
		//user supplied data
	UINT m_nPeriod;
		//timer period
	UINT m_nRes;
		//timer resolution

	//member variables for MMTIMERMODE_USERCALLBACK mode

	LPMMTIMERCALLBACK m_pfnCallback;
		//user supplied callback

	//member variables for MMTIMERMODE_POSTMESSAGE mode

	HWND	m_hWnd;
		//target window handle
	UINT	m_nMsg;
		//message
	WPARAM	m_wParam;
	LPARAM	m_lParam;
		//message params
};
//////////////////////////////////////////////////////////////////////
#endif 
//////////////////////////////////////////////////////////////////////
