// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "GraphRenderer.h"
/////////////////////////////////////////////////////////////////////////////
#include <float.h>
/////////////////////////////////////////////////////////////////////////////
CGraphRenderer::CGraphRenderer()
{
	m_lpBufferData	= NULL;
	m_dwBufferSize	= 0;
	m_dwBufferPos	= 0;
}
/////////////////////////////////////////////////////////////////////////////
CGraphRenderer::~CGraphRenderer()
{
	Destroy();
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::Create(DWORD dwColor, DWORD dwBufferSize)
{
	Destroy();
	
	m_pen.CreatePen(PS_SOLID, 0, dwColor);

	if (dwBufferSize)
	{
		m_lpBufferData = new float[dwBufferSize];

		for (DWORD index=0; index<dwBufferSize; index++)
			m_lpBufferData[index] = FLT_MAX;

		m_dwBufferSize = dwBufferSize;
	}
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::Destroy()
{
	if (m_lpBufferData)
		delete [] m_lpBufferData;

	m_lpBufferData	= NULL;
	m_dwBufferSize	= 0;
	m_dwBufferPos	= 0;

	if (m_pen.m_hObject)
		VERIFY(m_pen.DeleteObject());
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::RenderGraph(CDC *pDC, CRect rect, float fltMinY, float fltMaxY)
{
	if (m_lpBufferData)
	{
		int graphW			= rect.Width()	- BORDER_HORIZONTAL;
		int graphH			= rect.Height()	- BORDER_VERTICAL;
	
		float fltPixelRatio	= graphH / (fltMaxY - fltMinY);

		float fltYPrev		= FLT_MAX;

		CPen* pOldPen		= pDC->SelectObject(&m_pen);

		for (int index=0; index<graphW; index++)
		{
			float fltY = m_lpBufferData[(m_dwBufferPos - graphW + index) & (m_dwBufferSize - 1)];

			if ((fltY		!= FLT_MAX) && 
				(fltYPrev	!= FLT_MAX))
			{
				if (index)
				{
					if (fltY > fltMaxY)
						fltY = fltMaxY;

					int y0 = (int)((fltYPrev	- fltMinY) * fltPixelRatio);
					int y1 = (int)((fltY		- fltMinY) * fltPixelRatio);

					pDC->MoveTo(rect.left + BORDER_LEFT + index - 1	, rect.top + BORDER_TOP + graphH - 1 - y0);
					pDC->LineTo(rect.left + BORDER_LEFT + index		, rect.top + BORDER_TOP + graphH - 1 - y1);
				}
			}

			fltYPrev = fltY;
		}

		pDC->SelectObject(pOldPen);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::RenderLabels(CDC *pDC, CRect rect, float fltMinY, float fltMaxY, LPCSTR lpFormat, LPCSTR lpCaption, LPCSTR lpName)
{
	int graphW			= rect.Width()	- BORDER_HORIZONTAL;
	int graphH			= rect.Height()	- BORDER_VERTICAL;

	float fltPixelRatio	= graphH / (fltMaxY - fltMinY);

	CString strLabel;

	CSize	labelExtent;
	CRect	labelRect;

	//min	

	strLabel.Format(lpFormat, fltMinY);
	labelExtent	= pDC->GetTextExtent(strLabel);
	labelRect	= CRect(BORDER_LEFT - labelExtent.cx - 4, rect.Height() - BORDER_BOTTOM - 1 - labelExtent.cy/2, BORDER_LEFT - 4, rect.Height() - BORDER_BOTTOM - 1 + labelExtent.cy/2);
	pDC->TextOut(labelRect.left, labelRect.top, strLabel);

	//max

	strLabel.Format(lpFormat, fltMaxY);
	labelExtent	= pDC->GetTextExtent(strLabel);
	labelRect	= CRect(BORDER_LEFT - labelExtent.cx - 4, BORDER_TOP - labelExtent.cy/2, BORDER_LEFT - 4, BORDER_TOP + labelExtent.cy/2);
	pDC->TextOut(labelRect.left, labelRect.top, strLabel);

	//caption

	strLabel	= lpCaption;
	labelExtent	= pDC->GetTextExtent(strLabel);
	labelRect	= CRect(BORDER_LEFT, 0, BORDER_LEFT + labelExtent.cx, BORDER_TOP);
	pDC->DrawText(strLabel, strlen(strLabel), labelRect, 0);

	//name

	strLabel	= lpName;
	labelExtent	= pDC->GetTextExtent(strLabel);
	labelRect	= CRect(rect.Width() - BORDER_RIGHT - labelExtent.cx, 0, rect.Width() - BORDER_RIGHT, labelExtent.cy);
	pDC->TextOut(labelRect.left, labelRect.top, strLabel);
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::RenderCurrentValue(CDC *pDC, CRect rect, float fltMinY, float fltMaxY, LPCSTR lpFormat)
{
	if (m_lpBufferData)
	{
		float data			= m_lpBufferData[(m_dwBufferPos - 1) & (m_dwBufferSize - 1)];

		int graphW			= rect.Width()	- BORDER_HORIZONTAL;
		int graphH			= rect.Height()	- BORDER_VERTICAL;

		float fltPixelRatio	= graphH / (fltMaxY - fltMinY);

		if (data != FLT_MAX)
		{
			int y = (int)(BORDER_TOP + graphH - 1 - (data - fltMinY) * fltPixelRatio);
			if (y < BORDER_TOP)
				y = BORDER_TOP;
			if (y > BORDER_TOP + graphH)
				y = BORDER_TOP + graphH;

			CString strLabel;

			CSize	labelExtent;
			CRect	labelRect;

			strLabel.Format(lpFormat, data);
			labelExtent			= pDC->GetTextExtent(strLabel);
			labelRect			= CRect(rect.Width() - BORDER_RIGHT, y - labelExtent.cy/2, rect.Width(), y + labelExtent.cy/2);
			pDC->TextOut(labelRect.left, labelRect.top, strLabel);
		}
	}
}
/////////////////////////////////////////////////////////////////////////////
void CGraphRenderer::AppendData(float data)
{
	if (m_lpBufferData)
	{
		m_lpBufferData[m_dwBufferPos] = data;

		m_dwBufferPos = (m_dwBufferPos + 1) & (m_dwBufferSize - 1);
	}
}
/////////////////////////////////////////////////////////////////////////////
