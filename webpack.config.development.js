const { merge } = require('webpack-merge');
const commonConfig = require('./webpack.config.common');

const developmentConfig = {
  mode: 'development',
  devtool: 'source-map',
  // Other configurations specific to development mode...
};

module.exports = merge(commonConfig, developmentConfig);