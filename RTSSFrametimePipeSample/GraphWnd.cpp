// GraphWnd.cpp : implementation file
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "RTSSFrametimePipeSample.h"
#include "GraphWnd.h"

#include <float.h>
/////////////////////////////////////////////////////////////////////////////
// CGraphWnd
/////////////////////////////////////////////////////////////////////////////
IMPLEMENT_DYNAMIC(CGraphWnd, CWnd)
/////////////////////////////////////////////////////////////////////////////
CGraphWnd::CGraphWnd()
{
}
/////////////////////////////////////////////////////////////////////////////
CGraphWnd::~CGraphWnd()
{
	DestroyMemDC();
}
/////////////////////////////////////////////////////////////////////////////
BEGIN_MESSAGE_MAP(CGraphWnd, CWnd)
	ON_WM_SIZE()
	ON_WM_PAINT()
END_MESSAGE_MAP()
/////////////////////////////////////////////////////////////////////////////
// CGraphWnd message handlers
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::OnSize(UINT nType, int cx, int cy)
{
	CWnd::OnSize(nType, cx, cy);

	if (m_hWnd)
	{
		DestroyMemDC();

		CDC* pDC = GetDC();	
		PrepareMemDC(pDC); 
		ReleaseDC(pDC);

		RedrawWindow();
	}
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::PrepareMemDC(CDC *pDC)
{
	if (m_memDC.GetSafeHdc())
		return;

	CRect clientRect; GetClientRect(&clientRect);

	m_memDC.CreateCompatibleDC(pDC);
	m_bitmap.CreateCompatibleBitmap(pDC, clientRect.Width(), clientRect.Height());

	m_renderer.Create(GRAPH_COLOR, GRAPH_BUFFER_SIZE);

	m_font.CreateFont(-11, 0, 0, 0, FW_REGULAR, 0, 0, 0, DEFAULT_CHARSET, 0, 0, 0, 0, NULL);
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::DestroyMemDC()
{
	if (m_memDC.GetSafeHdc())
	{
		if (m_memDC.GetSafeHdc())
			VERIFY(m_memDC.DeleteDC());
	}

	if (m_bitmap.GetSafeHandle())
		VERIFY(m_bitmap.DeleteObject());

	m_renderer.Destroy();

	m_font.DeleteObject();
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::OnPaint()
{
	if (!m_memDC.GetSafeHdc())
	{
		CDC* pDC = GetDC();	
		PrepareMemDC(pDC); 
		ReleaseDC(pDC);
	}

	CRect updateRect; GetUpdateRect(&updateRect);

	CPaintDC dc(this);

	Draw(&dc, updateRect);
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::Draw(CDC *pDC, CRect updateRect)
{	
	//pre-calculate graph width, height, vertical limits and vertical pixel ratio

	CRect clientRect; GetClientRect(&clientRect);

	int graphW			= clientRect.Width()	- BORDER_HORIZONTAL;
	int graphH			= clientRect.Height()	- BORDER_VERTICAL;

	float fltMinY		= GRAPH_MIN;
	float fltMaxY		= GRAPH_MAX;
	float fltPixelRatio	= graphH / (fltMaxY - fltMinY);

	CBitmap* pBitmap = m_memDC.SelectObject(&m_bitmap);

	//clear background

	m_memDC.FillSolidRect(0, 0, clientRect.Width(), clientRect.Height(), 0);

	//render graph
		
	m_renderer.RenderGraph(&m_memDC, clientRect, fltMinY, fltMaxY);

	CFont* pOldFont = m_memDC.SelectObject(&m_font);

	m_memDC.SetTextColor(TEXT_COLOR);
	m_memDC.SetBkColor(BGND_COLOR);

	//render min/max/name labels

	m_renderer.RenderLabels(&m_memDC, clientRect, fltMinY, fltMaxY, GRAPH_OUTPUT_FORMAT, m_strCaption, GRAPH_NAME);

	//render current value

	m_renderer.RenderCurrentValue(&m_memDC, clientRect, fltMinY, fltMaxY, GRAPH_OUTPUT_FORMAT);

	m_memDC.SelectObject(pOldFont);

	//present rendered buffer

	pDC->BitBlt(updateRect.left, updateRect.top, updateRect.Width(), updateRect.Height(), &m_memDC,updateRect.left, updateRect.top, SRCCOPY);

	m_memDC.SelectObject(pBitmap);
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::AppendData(float data)
{
	m_renderer.AppendData(data);
}
/////////////////////////////////////////////////////////////////////////////
void CGraphWnd::SetCaption(LPCSTR lpCaption)
{
	m_strCaption = lpCaption;
}
/////////////////////////////////////////////////////////////////////////////
