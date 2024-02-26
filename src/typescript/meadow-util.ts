import { randomUUID } from 'crypto'
import * as SerialPort  from 'serialport';
import * as Fs from 'fs';
import * as os from 'os';

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

	constructor()
	{
		var thisExtension = vscode.extensions.getExtension('wildernesslabs.meadow');

		var extPath = thisExtension.extensionPath;

		const isDevelopment = process.env.NODE_ENV == "development";
		const debugOrRelease = isDevelopment ? "Debug" : "Release";

		this.UtilPath = path.join(extPath, 'src', 'csharp', 'bin', debugOrRelease, 'net7.0', 'vscode-meadow.dll');
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

		proc = await execa('dotnet', [ this.UtilPath ].concat(stdargs));

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

export function getTempFile(): string {
	var tempFileName = 'vscode-meadow-' + randomUUID() + '.txt'
	const tempFile = path.join(os.tmpdir(), tempFileName)
	return tempFile
}