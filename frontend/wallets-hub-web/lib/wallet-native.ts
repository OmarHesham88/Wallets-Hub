import { Capacitor, registerPlugin } from "@capacitor/core";
export type CaptureStatus={installationId:string;deviceName:string;paired:boolean;deviceId?:string;notificationAccess:boolean;smsAccess:boolean;listenerConnectedAt?:number;lastNotificationAt?:number;lastSmsAt?:number;lastWalletMatchAt?:number};
type CapturePlugin={getStatus():Promise<CaptureStatus>;configure(options:{deviceId:string;deviceToken:string}):Promise<void>;clearPairing():Promise<void>;openNotificationAccess():Promise<void>;openAppSettings():Promise<void>;requestPermissions(options:{permissions:string[]}):Promise<{sms?:string}>;scanRecentSms():Promise<{checked:number;matched:number}>};
export const WalletCapture=registerPlugin<CapturePlugin>("WalletCapture");
export const isNative=()=>Capacitor.isNativePlatform();
