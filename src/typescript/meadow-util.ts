import * as SerialPort  from 'serialport';
import * as Fs from 'fs';

interface CommandResponse<T> {
	id: string;
	command: string;
	error?: string;
	response?: T;
}

export interface SimpleResult {
	sucess: boolean;
}

export class DeviceData {
	name: string;
	serial: string;
	platform: string;
	version: string;
}

const path = require('path');
const execa = require('execa');

import * as vscode from 'vscode';

export class MeadowUtil
{
	public UtilPath: string;

	isUnix: boolean = true;

	constructor()
	{
		var thisExtension = vscode.extensions.getExtension('wildernesslabs.meadow');

		var os = require('os');

		var plat = os.platform();

		if (plat.indexOf('win32') >= 0)
			this.isUnix = false;

		var extPath = thisExtension.extensionPath;

		const isProduction = process.env.NODE_ENV === "production";
		const debugOrRelease = isProduction ? "Release" : "Debug";

		this.UtilPath = path.join(extPath, 'src', 'csharp', 'bin', debugOrRelease, 'net6.0', 'meadow-vscode.dll');
	}

	async RunCommand<TResult>(cmd: string, args: string[] = null)
	{
		
		var stdargs = [`util`, `-c=${cmd}`];
		
		if (args && args.length > 0)
		{
			for (var a in args)
				stdargs.push(a);
		}

		var proc: any;

		//if (this.isUnix)
			proc = await execa('dotnet', [ this.UtilPath ].concat(stdargs));
		//else
			//proc = await execa(this.UtilPath, stdargs);

		var txt = proc['stdout'];

		return JSON.parse(txt) as CommandResponse<TResult>;
	}

	public async GetDevices()
	{
		var r = await this.RunCommand<Array<DeviceData>>("devices");
		return r.response;
	}

	// SerialPort doesn't work on M1 Apple Silicon yet :(
	// public async GetDevices()
	// {
	// 	var ports = await SerialPort.list();

	// 	return ports.map(p => ({
	// 		name: p.productId ?? p.manufacturer ?? p.serialNumber,
	// 		serial: p.serialNumber
	// 	})) as DeviceData[];
	// }
}
