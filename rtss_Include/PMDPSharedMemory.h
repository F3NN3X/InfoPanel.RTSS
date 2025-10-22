/////////////////////////////////////////////////////////////////////////////
//
// This header file defines PresentMon data provider's shared memory format
//
/////////////////////////////////////////////////////////////////////////////
#ifndef _PMDP_SHARED_MEMORY_INCLUDED_
#define _PMDP_SHARED_MEMORY_INCLUDED_
/////////////////////////////////////////////////////////////////////////////
#define PMDP_STATUS_OK						0
#define PMDP_STATUS_INIT_FAILED				1
#define PMDP_STATUS_START_STREAM_FAILED		2
#define PMDP_STATUS_GET_FRAME_DATA_FAILED	3
/////////////////////////////////////////////////////////////////////////////
typedef struct PM_FRAME_DATA_V1
{
	char				Application[MAX_PATH];
	uint32_t			ProcessID;
	uint64_t			SwapChainAddress;
	uint32_t			Runtime;
	int32_t				SyncInterval;
	uint32_t			PresentFlags;
	uint32_t			Dropped;
	double				TimeInSeconds;
	double				msInPresentAPI;
	double				msBetweenPresents;
	uint32_t			AllowsTearing;
	uint32_t			PresentMode;
	double				msUntilRenderComplete;
	double				msUntilDisplayed;
	double				msBetweenDisplayChange;
	double				msUntilRenderStart;
	uint64_t			qpcTime;
	double				msSinceInput;
	double				msGpuActive;
	double				msGpuVideoActive;
} PM_FRAME_DATA_V1, LPPM_FRAME_DATA_V1;
/////////////////////////////////////////////////////////////////////////////
typedef struct PM_FRAME_DATA_V2
{
	uint64_t			CPUStart;
	double				Frametime;
	double				CPUBusy;
	double				CPUWait;
	double				GPULatency;
	double				GPUTime;
	double				GPUBusy;
	double				VideoBusy;
	double				GPUWait;
	double				DisplayLatency;
	double				DisplayedTime;
	double				AnimationError;
	double				ClickToPhotonLatency;
} PM_FRAME_DATA_V2, LPPM_FRAME_DATA_V2;
/////////////////////////////////////////////////////////////////////////////
typedef struct PMDP_FRAME_DATA
{
	PM_FRAME_DATA_V1	data1;
	PM_FRAME_DATA_V2	data2;
} PMDP_FRAME_DATA, *LPPMDP_FRAME_DATA;
/////////////////////////////////////////////////////////////////////////////
typedef struct PMDP_SHARED_MEMORY
{
	DWORD	dwSignature;
		//signature allows applications to verify status of shared memory

		//The signature can be set to:
		//'PMDP'	- data provider's memory is initialized and contains 
		//			valid data 
		//0xDEAD	- data provider's memory is marked for deallocation and
		//			no longer contain valid data
		//otherwise	the memory is not initialized
	DWORD	dwVersion;
		//structure version ((major<<16) + minor)
		//must be set to 0x0001xxxx for v1.x structure 

	DWORD	dwFrameArrEntrySize;
		//size of PM_FRAME_DATA for compatibility with future versions
	DWORD	dwFrameArrOffset;
		//offset of arrFrame array for compatibility with future versions
	DWORD	dwFrameArrSize;
		//size of arrFrame array for compatibility with future versions

	DWORD	dwFrameCount;
		//total frame count in ring buffer array
	DWORD	dwFramePos;
		//frame position in ring buffer array

	DWORD	dwStatus;
		//current PMDP_STATUS_XXX status

	PMDP_FRAME_DATA arrFrame[8192];
		//frame ring buffer array

} PMDP_SHARED_MEMORY, *LPPMDP_SHARED_MEMORY;
/////////////////////////////////////////////////////////////////////////////
#endif //_PMDP_SHARED_MEMORY_INCLUDED_