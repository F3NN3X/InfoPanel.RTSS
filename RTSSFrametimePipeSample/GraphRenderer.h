// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#pragma once
/////////////////////////////////////////////////////////////////////////////
#define BORDER_TOP				32
#define BORDER_BOTTOM			8
#define BORDER_VERTICAL			40

#define BORDER_LEFT				32
#define BORDER_RIGHT			32
#define BORDER_HORIZONTAL		64
/////////////////////////////////////////////////////////////////////////////
class CGraphRenderer
{
public:
	void Create(DWORD dwColor, DWORD dwBufferSize);
	void Destroy();

	void RenderGraph(CDC *pDC, CRect rect, float fltMinY, float fltMaxY);
	void RenderLabels(CDC *pDC, CRect rect, float fltMinY, float fltMaxY, LPCSTR lpFormat, LPCSTR lpCaption, LPCSTR lpName);
	void RenderCurrentValue(CDC *pDC, CRect rect, float fltMinY, float fltMaxY, LPCSTR lpFormat);
	
	void AppendData(float data);

	CGraphRenderer();
	virtual ~CGraphRenderer();

protected:
	CPen	m_pen;

	float*	m_lpBufferData;
	DWORD	m_dwBufferSize;
	DWORD	m_dwBufferPos;
};
/////////////////////////////////////////////////////////////////////////////
