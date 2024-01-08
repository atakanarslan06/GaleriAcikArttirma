import React from 'react'
import {useParams} from "react-router-dom"
import { useGetVehicleByIdQuery } from '../../Api/vehicleApi';
import { Loader } from '../../Helper';
import './Styles/VehicleDetail.css'
import BidsDetail from '../Bid/BidsDetail';
function VehicleDetail() {

        const {vehicleId} = useParams();
        const {data,isLoading} = useGetVehicleByIdQuery(vehicleId)    

        const safeVehicleId = vehicleId || ""
        var highBid = 20000;

        if (data) {
          if (data.result.bids.length > 0) {
            const valueResponse =  data.result.bids.slice().sort((a:any,b:any) => a -b )
            const higherBid = valueResponse[valueResponse.length-1].bidAmount
            highBid = higherBid;
          }
        
        }

      if (!data) {
        return (
        <Loader></Loader>

        )
      }
  return (
    <>
    <div className='auction-item text-center'>
        <img className='container' src={data.result.image} ></img>
        <h2> Marka Modeli : {data.result.brandAndModel} </h2>
        <p>Hakkında: {data.result.additionalInformation} </p>
        <p> Mevcut Teklif: {highBid}$ </p>
    </div>

    <BidsDetail vehicleId={safeVehicleId} ></BidsDetail>
    </>
  )




}

export default VehicleDetail
