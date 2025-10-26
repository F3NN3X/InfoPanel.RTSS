// RTSSFrametimePipeSample.h : main header file for the RTSSFrametimePipeSAMPLE application
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#ifndef _RTSSFRAMETIMEPIPESAMPLE_H_INCLUDED_
#define _RTSSFRAMETIMEPIPESAMPLE_H_INCLUDED_
/////////////////////////////////////////////////////////////////////////////
#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000
/////////////////////////////////////////////////////////////////////////////
#ifndef __AFXWIN_H__
	#error include 'stdafx.h' before including this file for PCH
#endif

#include "resource.h"		// main symbols
/////////////////////////////////////////////////////////////////////////////
// CRTSSFrametimePipeSampleApp:
// See RTSSFrametimePipeSample.cpp for the implementation of this class
//
/////////////////////////////////////////////////////////////////////////////
class CRTSSFrametimePipeSampleApp : public CWinApp
{
public:
	CRTSSFrametimePipeSampleApp();

// Overrides
	// ClassWizard generated virtual function overrides
	//{{AFX_VIRTUAL(CRTSSFrametimePipeSampleApp)
	public:
	virtual BOOL InitInstance();
	//}}AFX_VIRTUAL

// Implementation

	//{{AFX_MSG(CRTSSFrametimePipeSampleApp)
		// NOTE - the ClassWizard will add and remove member functions here.
		//    DO NOT EDIT what you see in these blocks of generated code !
	//}}AFX_MSG
	DECLARE_MESSAGE_MAP()
};


/////////////////////////////////////////////////////////////////////////////

//{{AFX_INSERT_LOCATION}}
// Microsoft Visual C++ will insert additional declarations immediately before the previous line.

#endif 
