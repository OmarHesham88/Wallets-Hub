import { Capacitor, registerPlugin } from "@capacitor/core";
export type CaptureStatus={installationId:string;deviceName:string;paired:boolean;deviceId?:string;notificationAccess:boolean;listenerConnectedAt?:number;lastNotificationAt?:number;lastWalletMatchAt?:number};
type CapturePlugin={getStatus():Promise<CaptureStatus>;configure(options:{deviceId:string;deviceToken:string}):Promise<void>;clearPairing():Promise<void>;openNotificationAccess():Promise<void>;openAppSettings():Promise<void>};
export const WalletCapture=registerPlugin<CapturePlugin>("WalletCapture");
export const isNative=()=>Capacitor.isNativePlatform();
