import React, { useState } from 'react'
import { useCreateBidMutation } from '../../Api/bidApi'
import { bidModel } from '../../Interfaces/bidModel';
import userModel from '../../Interfaces/userModel';
import { useSelector } from 'react-redux';
import { RootState } from '../../Storage/store';
import { create } from 'domain';
import { useDispatch } from 'react-redux';
import { setBidChange } from '../../Storage/Redux/bidSlice';
import { ToastrNotify } from '../../Helper';
import { error } from 'console';
function CreateBid(props:{vehicleId:number}) {

  const [createBid] = useCreateBidMutation();


    const userStore : userModel = useSelector((state:RootState) => state.authenticationStore);
    const Dispatch = useDispatch();

    const [bidAmount,setBidAmountState] = useState("");

  const bidModel : bidModel = {
    bidAmount : parseInt(bidAmount),
    userId : userStore.nameid!,
    vehicleId: props.vehicleId,
  }



  const handleCreateBid = () =>{
    createBid(bidModel).then((response:any) => {
      
      
        if (response.data.isSuccess === true) {
          Dispatch(setBidChange(bidModel.bidAmount))
          ToastrNotify("Teklinifiz Başarılı","success");
      }
    
      if (response.data.isSuccess === false) {
        ToastrNotify(response.data.errorMessages[0],"error");
      }
    })
  }




  return (
    <div className='container' >
        <form>
            <label htmlFor='bidAmount' >Teklif Tutarı : </label>
            <br></br>
            <input type='number' className='form-control' id='bidAmount' name='bidAmount' onChange={(e)=>setBidAmountState(e.target.value)} />
                <div className='text-center mb-3 ' >
                  <br></br>
                    <button type='button' onClick={()=>handleCreateBid()}  >Teklif Ver</button>
                </div>
        </form>
    </div>
  )
}

export default CreateBid
