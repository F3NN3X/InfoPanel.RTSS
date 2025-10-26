// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#pragma once
/////////////////////////////////////////////////////////////////////////////
// CGraphWnd
/////////////////////////////////////////////////////////////////////////////
#include "GraphRenderer.h"
/////////////////////////////////////////////////////////////////////////////
#define TEXT_COLOR			0xFFFFFF
#define BGND_COLOR			0x000000
#define GRAPH_COLOR			0x008000
#define GRAPH_BUFFER_SIZE	4096
#define GRAPH_MIN			0.0f
#define GRAPH_MAX			50.0f
#define GRAPH_NAME			"Frametime, ms"
#define GRAPH_OUTPUT_FORMAT	"%.1f"
/////////////////////////////////////////////////////////////////////////////
class CGraphWnd : public CWnd
{
	DECLARE_DYNAMIC(CGraphWnd)

public:
	void PrepareMemDC(CDC *pDC);
	void DestroyMemDC();

	void AppendData(float data);
	void SetCaption(LPCSTR lpCaption);

	void Draw(CDC *pDC, CRect updateRect);

	CGraphWnd();
	virtual ~CGraphWnd();

protected:
	DECLARE_MESSAGE_MAP()
	
public:
	afx_msg void OnSize(UINT nType, int cx, int cy);
	afx_msg void OnPaint();

protected:
	CDC				m_memDC;
	CBitmap			m_bitmap;

	CGraphRenderer	m_renderer;
	CFont			m_font;

	CString			m_strCaption;
};
/////////////////////////////////////////////////////////////////////////////

