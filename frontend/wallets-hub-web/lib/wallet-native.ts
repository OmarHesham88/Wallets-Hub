import { Capacitor, registerPlugin } from "@capacitor/core";
import { useSyncExternalStore } from "react";
export type CaptureStatus={installationId:string;deviceName:string;paired:boolean;deviceId?:string;smsAccess:boolean;batteryOptimizationIgnored:boolean;lastSmsAt?:number;lastWalletMatchAt?:number};
type CapturePlugin={getStatus():Promise<CaptureStatus>;configure(options:{deviceId:string;deviceToken:string}):Promise<void>;clearPairing():Promise<void>;openAppSettings():Promise<void>;openBatterySettings():Promise<void>;requestPermissions(options:{permissions:string[]}):Promise<{sms?:string}>;scanRecentSms():Promise<{checked:number;matched:number}>};
export const WalletCapture=registerPlugin<CapturePlugin>("WalletCapture");
export const isNative=()=>Capacitor.isNativePlatform();
const subscribeNative = () => () => undefined;
export const useIsNative = () => useSyncExternalStore(subscribeNative, isNative, () => false);
