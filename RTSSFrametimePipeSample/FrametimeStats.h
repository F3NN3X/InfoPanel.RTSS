// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#pragma once
/////////////////////////////////////////////////////////////////////////////
#define STATS_FPS_AVG_BUFFER_SIZE										1024
#define STATS_FPS_LOW_BUFFER_SIZE										1024
#define STATS_FPS_AVERAGING_INTERVAL									1000
#define PEAK_FRAMERATE_CALC_AVERAGE										0
#define PEAK_FRAMERATE_CALC_INSTANTANEOUS								1
#define PERCENTILE_CALC_INTEGRAL										0
#define PERCENTILE_CALC_LINEAR											1
/////////////////////////////////////////////////////////////////////////////
class CFrametimeStats
{
public:
	CFrametimeStats();
	virtual ~CFrametimeStats();

	void Create(DWORD dwFpsAvgBufferSize, DWORD dwFpsLowBufferSize);
	void Destroy();

	void AppendData(DWORD dwFrametime);

	DWORDLONG	GetTotalTime();
	DWORD		GetFrameCount();

	float		GetCurFramerate();
	float		GetMinFramerate();
	float		GetAvgFramerate();
	float		GetMaxFramerate();
	float		Get1Dot0PercentLowFramerate();
	float		Get0Dot1PercentLowFramerate();

protected:
	LPDWORD		m_lpFpsAvgBufferData;
	DWORD		m_dwFpsAvgBufferSize;
	DWORD		m_dwFpsAvgBufferPos;

	LPDWORD		m_lpFpsLowBufferData;
	DWORD		m_dwFpsLowBufferSize;

	DWORDLONG	m_qwTotalTime;
	DWORD		m_dwFrameCount;

	float		m_fltCurFramerate;
	float		m_fltMinFramerate;
	float		m_fltMaxFramerate;
	float		m_fltAvgFramerate;
	float		m_flt1Dot0PercentLowFramerate;
	float		m_flt0Dot1PercentLowFramerate;

	long		GetInsertPos(LPDWORD lpData, DWORD dwSize, DWORD dwData);
	float		CalcFramerate();

	DWORD		m_dwPeakFramerateCalc;
	DWORD		m_dwPercentileCalc;
};
/////////////////////////////////////////////////////////////////////////////
