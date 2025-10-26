// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "FrametimeStats.h"
/////////////////////////////////////////////////////////////////////////////
#include <float.h>
/////////////////////////////////////////////////////////////////////////////
CFrametimeStats::CFrametimeStats()
{
	m_lpFpsAvgBufferData			= NULL;
	m_dwFpsAvgBufferSize			= 0;
	m_dwFpsAvgBufferPos				= 0;

	m_lpFpsLowBufferData			= NULL;
	m_dwFpsLowBufferSize			= 0;

	m_qwTotalTime					= 0;
	m_dwFrameCount					= 0;

	m_fltCurFramerate				= 0.0f;
	m_fltMinFramerate				= FLT_MAX;
	m_fltMaxFramerate				= 0.0f;
	m_fltAvgFramerate				= 0.0f;
	m_flt1Dot0PercentLowFramerate	= 0.0f;
	m_flt0Dot1PercentLowFramerate	= 0.0f;

	m_dwPeakFramerateCalc			= PEAK_FRAMERATE_CALC_AVERAGE;
	m_dwPercentileCalc				= PERCENTILE_CALC_INTEGRAL;
}
/////////////////////////////////////////////////////////////////////////////
CFrametimeStats::~CFrametimeStats()
{
	Destroy();
}
/////////////////////////////////////////////////////////////////////////////
void CFrametimeStats::Create(DWORD dwFpsAvgBufferSize, DWORD dwFpsLowBufferSize)
{
	Destroy();

	if (dwFpsAvgBufferSize)
	{
		m_lpFpsAvgBufferData = new DWORD[dwFpsAvgBufferSize];
		ZeroMemory(m_lpFpsAvgBufferData, dwFpsAvgBufferSize * sizeof(DWORD));

		m_dwFpsAvgBufferSize = dwFpsAvgBufferSize;
	}

	if (dwFpsLowBufferSize)
	{
		m_lpFpsLowBufferData = new DWORD[dwFpsLowBufferSize];
		ZeroMemory(m_lpFpsLowBufferData, dwFpsLowBufferSize * sizeof(DWORD));

		m_dwFpsLowBufferSize = dwFpsLowBufferSize;
	}
}
/////////////////////////////////////////////////////////////////////////////
void CFrametimeStats::Destroy()
{
	if (m_lpFpsAvgBufferData)
		delete [] m_lpFpsAvgBufferData;
	m_lpFpsAvgBufferData			= NULL;
	m_dwFpsAvgBufferSize			= 0;
	m_dwFpsAvgBufferPos				= 0;

	if (m_lpFpsLowBufferData)
		delete [] m_lpFpsLowBufferData;
	m_lpFpsLowBufferData			= NULL;
	m_dwFpsLowBufferSize			= 0;

	m_qwTotalTime					= 0;
	m_dwFrameCount					= 0;
	m_fltCurFramerate				= 0.0f;
	m_fltMinFramerate				= FLT_MAX;
	m_fltMaxFramerate				= 0.0f;
	m_fltAvgFramerate				= 0.0f;
	m_flt1Dot0PercentLowFramerate	= 0.0f;
	m_flt0Dot1PercentLowFramerate	= 0.0f;
}
/////////////////////////////////////////////////////////////////////////////
long CFrametimeStats::GetInsertPos(LPDWORD lpData, DWORD dwSize, DWORD dwData)
{
	if (lpData)
	{
		long pos0 = 0;
		long pos1 = dwSize;

		while (pos0 <= pos1)
		{
			long pos = pos0 + ((pos1 - pos0)>>1);

			if (lpData[pos] == dwData)
				return pos;
			else
			if (lpData[pos] < dwData)
				pos1 = pos - 1;
			else
				pos0 = pos + 1;
		}

		return pos0;
	}

	return -1;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::CalcFramerate()
{
	DWORD dwTime	= 0;
	DWORD dwFrame	= 0;

	for (dwFrame=0; dwFrame<m_dwFpsAvgBufferSize; dwFrame++)
	{
		DWORD dwFramePos	= (m_dwFpsAvgBufferPos - dwFrame - 1) & (m_dwFpsAvgBufferSize - 1);
		DWORD dwFrameTime	= m_lpFpsAvgBufferData[dwFramePos];

		if (!dwFrameTime)
		{
			if (dwFrame)
				dwFrame--;

			break;
		}
 
		dwTime = dwTime + dwFrameTime;

		if (dwTime >= STATS_FPS_AVERAGING_INTERVAL * 1000)
			break;
	}

	if (dwTime)
		return 1000000.0f * (dwFrame + 1) / dwTime;

	return 0.0f;
}
/////////////////////////////////////////////////////////////////////////////
void CFrametimeStats::AppendData(DWORD dwFrametime)
{
	if (m_lpFpsAvgBufferData)
	{
		m_lpFpsAvgBufferData[m_dwFpsAvgBufferPos] = dwFrametime;

		m_dwFpsAvgBufferPos = (m_dwFpsAvgBufferPos + 1) & (m_dwFpsAvgBufferSize - 1);
	}

	m_fltCurFramerate = CalcFramerate();

	if (m_dwPeakFramerateCalc == PEAK_FRAMERATE_CALC_INSTANTANEOUS)
		//calculate peak (min/max) FPS using instantaneous framerate
	{
		if (dwFrametime)
		{
			float fltFramerate = 1000000.0f / dwFrametime;

			if (m_fltMinFramerate > fltFramerate)
				m_fltMinFramerate = fltFramerate;
			if (m_fltMaxFramerate < fltFramerate)
				m_fltMaxFramerate = fltFramerate;
		}
	}
	else
		//calculate peak (min/max) FPS using averaged framerate
	{
		if (m_fltMinFramerate > m_fltCurFramerate)
			m_fltMinFramerate = m_fltCurFramerate;
		if (m_fltMaxFramerate < m_fltCurFramerate)
			m_fltMaxFramerate = m_fltCurFramerate;
	}

	m_qwTotalTime += dwFrametime;
	m_dwFrameCount++;

	//append collection of slowest frames sorted by descending, we keep the set of 1024 slowes frames to calculate and 1%/0.1% fps lows in realtime

	long pos = GetInsertPos(m_lpFpsLowBufferData, m_dwFpsLowBufferSize, dwFrametime);

	if ((pos != -1) && (pos < m_dwFpsLowBufferSize))
	{
		if (pos < m_dwFpsLowBufferSize - 1)
			MoveMemory(m_lpFpsLowBufferData + pos + 1, m_lpFpsLowBufferData + pos, (m_dwFpsLowBufferSize - pos - 1) * sizeof(DWORD));

		m_lpFpsLowBufferData[pos] = dwFrametime;
	}

	DWORD dw1Dot0PercentLowFrametime	= 0;
	DWORD dw0Dot1PercentLowFrametime	= 0;

	if (m_dwPercentileCalc == PERCENTILE_CALC_LINEAR)
		//linear percentile calculation:
		//1% low frametime is taken on the boundary of N slowest frames, where N is 1% of total benchmark frame count
	{
		DWORD dw1Dot0PercentLowIndex = min(STATS_FPS_LOW_BUFFER_SIZE - 1, m_dwFrameCount / 100	);
		DWORD dw0Dot1PercentLowIndex = min(STATS_FPS_LOW_BUFFER_SIZE - 1, m_dwFrameCount / 1000	);

		dw1Dot0PercentLowFrametime = m_lpFpsLowBufferData[dw1Dot0PercentLowIndex];
		dw0Dot1PercentLowFrametime = m_lpFpsLowBufferData[dw0Dot1PercentLowIndex];
	}
	else
		//integral percentile calculation:
		//1% low frametime is taken on the boundary of N slowest frames, where N is a set of frames taking 1% of total benchmark time
	{
		DWORDLONG qwTime					= 0;
		DWORDLONG qw1Dot0PercentLowTime		= m_qwTotalTime / 100;
		DWORDLONG qw0Dot1PercentLowTime		= m_qwTotalTime / 1000;

		for (DWORD index=0; index<m_dwFpsLowBufferSize; index++)
		{
			DWORD dwFrametime = m_lpFpsLowBufferData[index];

			if (!dw1Dot0PercentLowFrametime)
			{
				if (qwTime + dwFrametime > qw1Dot0PercentLowTime)
					dw1Dot0PercentLowFrametime = dwFrametime;
			}

			if (!dw0Dot1PercentLowFrametime)
			{
				if (qwTime + dwFrametime > qw0Dot1PercentLowTime)
					dw0Dot1PercentLowFrametime = dwFrametime;
			}

			qwTime += dwFrametime;

			if (dw1Dot0PercentLowFrametime && dw0Dot1PercentLowFrametime)
				break;
		}
	}

	//invert 1% and 0.1% low frametimes to calculate 1% and 0.1% low framerates

	if (dw1Dot0PercentLowFrametime)
		m_flt1Dot0PercentLowFramerate = 1000000.0f / dw1Dot0PercentLowFrametime;
	if (dw0Dot1PercentLowFrametime)
		m_flt0Dot1PercentLowFramerate = 1000000.0f / dw0Dot1PercentLowFrametime;
}
/////////////////////////////////////////////////////////////////////////////
DWORDLONG CFrametimeStats::GetTotalTime()
{
	return m_qwTotalTime;
}
/////////////////////////////////////////////////////////////////////////////
DWORD CFrametimeStats::GetFrameCount()
{
	return m_dwFrameCount;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::GetCurFramerate()
{
	return m_fltCurFramerate;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::GetMinFramerate()
{
	return m_fltMinFramerate;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::GetAvgFramerate()
{
	if (m_qwTotalTime)
		return 1000000.0f * m_dwFrameCount / m_qwTotalTime;

	return 0;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::GetMaxFramerate()
{
	return m_fltMaxFramerate;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::Get1Dot0PercentLowFramerate()
{
	return m_flt1Dot0PercentLowFramerate;
}
/////////////////////////////////////////////////////////////////////////////
float CFrametimeStats::Get0Dot1PercentLowFramerate()
{
	return m_flt0Dot1PercentLowFramerate;
}
/////////////////////////////////////////////////////////////////////////////
